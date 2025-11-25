import { PrismaClient, Role, LeaveType, BookingSource, BookingType } from '@prisma/client';
import bcrypt from 'bcryptjs';

const prisma = new PrismaClient();

async function main() {
  const password = await bcrypt.hash('admin123', 10);
  const admin = await prisma.user.upsert({
    where: { email: 'admin@example.com' },
    update: {},
    create: { email: 'admin@example.com', name: 'Admin', password, role: Role.ADMIN }
  });

  const schedule = await prisma.workSchedule.upsert({
    where: { id: 1 },
    update: {},
    create: {
      name: 'Standard 40h',
      monHours: 8,
      tueHours: 8,
      wedHours: 8,
      thuHours: 8,
      friHours: 8,
      satHours: 0,
      sunHours: 0,
      breakRule: '30 minutes after 6h'
    }
  });

  const employee = await prisma.employee.upsert({
    where: { personnelNumber: 'E-1001' },
    update: {},
    create: {
      personnelNumber: 'E-1001',
      name: 'Erika Muster',
      email: 'erika@example.com',
      department: 'Engineering',
      location: 'Berlin',
      workScheduleId: schedule.id,
      startDate: new Date(),
      role: Role.EMPLOYEE,
      userId: admin.id
    }
  });

  await prisma.timeEntry.createMany({
    data: [
      {
        employeeId: employee.id,
        type: BookingType.KOMMEN,
        source: BookingSource.WEB,
        timestampUtc: new Date()
      },
      {
        employeeId: employee.id,
        type: BookingType.GEHEN,
        source: BookingSource.WEB,
        timestampUtc: new Date(new Date().getTime() + 8 * 60 * 60 * 1000)
      }
    ],
    skipDuplicates: true
  });

  await prisma.leaveRequest.createMany({
    data: [
      {
        employeeId: employee.id,
        startDate: new Date(),
        endDate: new Date(new Date().getTime() + 2 * 24 * 60 * 60 * 1000),
        leaveType: LeaveType.VACATION
      }
    ],
    skipDuplicates: true
  });

  console.log('Seed finished', { admin: admin.email, schedule: schedule.name });
}

main().finally(async () => prisma.$disconnect());
