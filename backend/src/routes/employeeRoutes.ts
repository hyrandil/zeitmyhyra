import { Router } from 'express';
import { authenticate, requireRole } from '../middleware/auth';
import { Role } from '@prisma/client';
import { createEmployee, deleteEmployee, getEmployee, listEmployees, updateEmployee } from '../controllers/employeeController';

const router = Router();

router.use(authenticate);
router.get('/', listEmployees);
router.get('/:id', getEmployee);
router.post('/', requireRole([Role.HR, Role.ADMIN]), createEmployee);
router.put('/:id', requireRole([Role.HR, Role.ADMIN]), updateEmployee);
router.delete('/:id', requireRole([Role.HR, Role.ADMIN]), deleteEmployee);

export default router;
