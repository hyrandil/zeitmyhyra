import { Response } from 'express';
import { prisma } from '../utils/prisma';
import { LeaveStatus, LeaveType } from '@prisma/client';
import { z } from 'zod';
import { AuthRequest } from '../middleware/auth';
import { canAccessEmployee, getEmployeeIdForUser } from '../utils/access';

const requestSchema = z.object({
  employeeId: z.number(),
  startDate: z.string(),
  endDate: z.string(),
  leaveType: z.nativeEnum(LeaveType),
  comment: z.string().optional()
});

export const createLeaveRequest = async (req: AuthRequest, res: Response) => {
  if (!req.user) return res.status(401).json({ message: 'Unauthenticated' });
  const parsed = requestSchema.safeParse(req.body);
  if (!parsed.success) return res.status(400).json(parsed.error);
  const data = parsed.data;
  const employeeId = data.employeeId ?? (await getEmployeeIdForUser(req.user.id));
  if (!employeeId) return res.status(400).json({ message: 'No employee mapped to user' });
  if (!(await canAccessEmployee(req.user, employeeId))) return res.status(403).json({ message: 'Forbidden for this employee' });
  const leave = await prisma.leaveRequest.create({
    data: {
      employeeId,
      startDate: new Date(data.startDate),
      endDate: new Date(data.endDate),
      leaveType: data.leaveType,
      comment: data.comment
    }
  });
  res.status(201).json(leave);
};

export const listMyLeaveRequests = async (req: AuthRequest, res: Response) => {
  if (!req.user) return res.status(401).json({ message: 'Unauthenticated' });
  const employeeId = Number(req.params.employeeId) || (await getEmployeeIdForUser(req.user.id));
  if (!employeeId) return res.status(400).json({ message: 'No employee mapped to user' });
  if (!(await canAccessEmployee(req.user, employeeId))) return res.status(403).json({ message: 'Forbidden for this employee' });
  const requests = await prisma.leaveRequest.findMany({ where: { employeeId } });
  res.json(requests);
};

const reviewSchema = z.object({ status: z.nativeEnum(LeaveStatus), reviewer: z.number().optional() });

export const reviewLeave = async (req: AuthRequest, res: Response) => {
  const id = Number(req.params.id);
  const parsed = reviewSchema.safeParse(req.body);
  if (!parsed.success) return res.status(400).json(parsed.error);
  const { status, reviewer } = parsed.data;
  const updated = await prisma.leaveRequest.update({
    where: { id },
    data: { status, reviewedBy: reviewer }
  });
  res.json(updated);
};
