import { Router } from 'express';
import { authenticate, requireRole } from '../middleware/auth';
import { createLeaveRequest, listMyLeaveRequests, reviewLeave } from '../controllers/leaveController';
import { Role } from '@prisma/client';

const router = Router();

router.use(authenticate);
router.post('/', createLeaveRequest);
router.get('/:employeeId', listMyLeaveRequests);
router.post('/:id/review', requireRole([Role.TEAM_LEAD, Role.HR, Role.ADMIN]), reviewLeave);

export default router;
