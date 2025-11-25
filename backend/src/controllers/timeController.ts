import { Response } from 'express';
import { BookingSource, BookingType } from '@prisma/client';
import { prisma } from '../utils/prisma';
import { AuthRequest } from '../middleware/auth';
import { calculateDailySummary } from '../services/timeCalculation';
import { canAccessEmployee, getEmployeeIdForUser } from '../utils/access';
import { z } from 'zod';

const stampSchema = z.object({
  employeeId: z.number().optional(),
  type: z.nativeEnum(BookingType),
  source: z.nativeEnum(BookingSource).optional(),
  latitude: z.number().optional(),
  longitude: z.number().optional()
});

export const createStamp = async (req: AuthRequest, res: Response) => {
  if (!req.user) return res.status(401).json({ message: 'Unauthenticated' });
  const parsed = stampSchema.safeParse(req.body);
  if (!parsed.success) return res.status(400).json(parsed.error);

  const body = parsed.data;
  const targetEmployeeId = body.employeeId ?? (await getEmployeeIdForUser(req.user.id));
  if (!targetEmployeeId) return res.status(400).json({ message: 'No employee mapped to user' });

  if (!(await canAccessEmployee(req.user, targetEmployeeId))) {
    return res.status(403).json({ message: 'Forbidden for this employee' });
  }

  const entry = await prisma.timeEntry.create({
    data: {
      employeeId: targetEmployeeId,
      type: body.type,
      source: body.source || BookingSource.WEB,
      latitude: body.latitude,
      longitude: body.longitude,
      timestampUtc: new Date()
    }
  });
  res.status(201).json(entry);
};

const monthSchema = z.object({
  employeeId: z.number().optional(),
  month: z
    .string()
    .regex(/^\d{4}-\d{2}$/)
    .transform((val) => val)
});

export const monthlyOverview = async (req: AuthRequest, res: Response) => {
  if (!req.user) return res.status(401).json({ message: 'Unauthenticated' });
  const parsed = monthSchema.safeParse({ employeeId: req.query.employeeId ? Number(req.query.employeeId) : undefined, month: req.query.month });
  if (!parsed.success) return res.status(400).json(parsed.error);

  const employeeId = parsed.data.employeeId ?? (await getEmployeeIdForUser(req.user.id));
  if (!employeeId) return res.status(400).json({ message: 'No employee mapped to user' });
  if (!(await canAccessEmployee(req.user, employeeId))) return res.status(403).json({ message: 'Forbidden for this employee' });

  const [year, monthNumber] = parsed.data.month.split('-').map(Number);
  const start = new Date(Date.UTC(year, monthNumber - 1, 1));
  const end = new Date(Date.UTC(year, monthNumber, 0, 23, 59, 59));

  const entries = await prisma.timeEntry.findMany({
    where: { employeeId, timestampUtc: { gte: start, lte: end } },
    orderBy: { timestampUtc: 'asc' }
  });

  const summary = calculateDailySummary(entries);
  res.json({ entries, summary });
};
