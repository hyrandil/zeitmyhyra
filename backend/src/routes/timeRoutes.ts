import { Router } from 'express';
import { authenticate } from '../middleware/auth';
import { createStamp, monthlyOverview } from '../controllers/timeController';

const router = Router();

router.use(authenticate);
router.post('/stamp', createStamp);
router.get('/monthly', monthlyOverview);

export default router;
