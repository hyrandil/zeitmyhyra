import { Router } from 'express';
import { authenticate } from '../middleware/auth';
import { monthlyReport, overtimeReport } from '../controllers/reportController';

const router = Router();
router.use(authenticate);
router.get('/monthly', monthlyReport);
router.get('/overtime', overtimeReport);
export default router;
