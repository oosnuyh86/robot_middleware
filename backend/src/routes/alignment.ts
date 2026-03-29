import { Router } from "express";
import * as ctrl from "../controllers/alignment.controller.js";

const router = Router();

router.post("/locate-tool", ctrl.locateTool);
router.post("/compute-transform", ctrl.computeTransform);
router.post("/verify", ctrl.verify);

export default router;
