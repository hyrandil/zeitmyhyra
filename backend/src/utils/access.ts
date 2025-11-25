import { Role } from '@prisma/client';
import { prisma } from './prisma';

export const getEmployeeIdForUser = async (userId: number): Promise<number | null> => {
  const employee = await prisma.employee.findFirst({ where: { userId } });
  return employee?.id ?? null;
};

export const canAccessEmployee = async (user: { id: number; role: Role }, targetEmployeeId: number) => {
  if ([Role.ADMIN, Role.HR, Role.TEAM_LEAD].includes(user.role)) return true;
  const ownEmployeeId = await getEmployeeIdForUser(user.id);
  return ownEmployeeId === targetEmployeeId;
};
