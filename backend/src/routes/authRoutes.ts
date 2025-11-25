import { Router } from 'express';
import { login, me, register } from '../controllers/authController';
import { authenticate, requireRole } from '../middleware/auth';
import { Role } from '@prisma/client';

const router = Router();

router.post('/login', login);
router.post('/register', authenticate, requireRole([Role.ADMIN]), register);
router.get('/me', authenticate, me);

export default router;
