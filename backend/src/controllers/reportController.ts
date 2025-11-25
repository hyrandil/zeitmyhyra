import { Response } from 'express';
import { prisma } from '../utils/prisma';
import { calculateDailySummary } from '../services/timeCalculation';
import { AuthRequest } from '../middleware/auth';
import { canAccessEmployee, getEmployeeIdForUser } from '../utils/access';
import { z } from 'zod';

const monthlySchema = z.object({
  employeeId: z.number().optional(),
  month: z.string().regex(/^\d{4}-\d{2}$/)
});

export const monthlyReport = async (req: AuthRequest, res: Response) => {
  if (!req.user) return res.status(401).json({ message: 'Unauthenticated' });
  const parsed = monthlySchema.safeParse({
    employeeId: req.query.employeeId ? Number(req.query.employeeId) : undefined,
    month: req.query.month
  });
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

export const overtimeReport = async (req: AuthRequest, res: Response) => {
  if (!req.user) return res.status(401).json({ message: 'Unauthenticated' });
  const start = new Date(req.query.start as string);
  const end = new Date(req.query.end as string);
  const entries = await prisma.timeEntry.findMany({
    where: { timestampUtc: { gte: start, lte: end } },
    orderBy: { timestampUtc: 'asc' }
  });

  const grouped = entries.reduce<Record<number, typeof entries>>((acc, entry) => {
    acc[entry.employeeId] = acc[entry.employeeId] || [];
    acc[entry.employeeId].push(entry);
    return acc;
  }, {});

  const overtime = await Promise.all(
    Object.entries(grouped).map(async ([employeeId, items]) => {
      const numericId = Number(employeeId);
      const allowed = await canAccessEmployee(req.user!, numericId);
      if (!allowed) return null;
      return { employeeId: numericId, summary: calculateDailySummary(items) };
    })
  );

  res.json(overtime.filter(Boolean));
};
