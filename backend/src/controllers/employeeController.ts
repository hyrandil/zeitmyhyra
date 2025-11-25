import { Request, Response } from 'express';
import { prisma } from '../utils/prisma';
import { z } from 'zod';
import { Role } from '@prisma/client';

const employeeSchema = z.object({
  personnelNumber: z.string(),
  name: z.string(),
  email: z.string().email(),
  location: z.string(),
  department: z.string(),
  workScheduleId: z.number(),
  startDate: z.string(),
  endDate: z.string().optional(),
  role: z.nativeEnum(Role),
  userId: z.number().optional()
});

export const listEmployees = async (_req: Request, res: Response) => {
  const employees = await prisma.employee.findMany({ include: { workSchedule: true } });
  res.json(employees);
};

export const getEmployee = async (req: Request, res: Response) => {
  const id = Number(req.params.id);
  const employee = await prisma.employee.findUnique({ where: { id }, include: { workSchedule: true } });
  if (!employee) return res.status(404).json({ message: 'Not found' });
  res.json(employee);
};

export const createEmployee = async (req: Request, res: Response) => {
  const parsed = employeeSchema.safeParse(req.body);
  if (!parsed.success) return res.status(400).json(parsed.error);
  const data = parsed.data;
  const employee = await prisma.employee.create({
    data: {
      ...data,
      startDate: new Date(data.startDate),
      endDate: data.endDate ? new Date(data.endDate) : undefined
    }
  });
  res.status(201).json(employee);
};

export const updateEmployee = async (req: Request, res: Response) => {
  const id = Number(req.params.id);
  const parsed = employeeSchema.partial().safeParse(req.body);
  if (!parsed.success) return res.status(400).json(parsed.error);
  const data = parsed.data;
  const employee = await prisma.employee.update({
    where: { id },
    data: {
      ...data,
      startDate: data.startDate ? new Date(data.startDate) : undefined,
      endDate: data.endDate ? new Date(data.endDate) : undefined
    }
  });
  res.json(employee);
};

export const deleteEmployee = async (req: Request, res: Response) => {
  const id = Number(req.params.id);
  await prisma.employee.delete({ where: { id } });
  res.status(204).send();
};
