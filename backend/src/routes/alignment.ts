import { Router } from "express";
import * as ctrl from "../controllers/alignment.controller.js";

const router = Router();

router.post("/compute", ctrl.compute);
router.post("/verify", ctrl.verify);

export default router;
