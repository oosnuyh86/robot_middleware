import { Router } from "express";
import * as ctrl from "../controllers/uploads.controller.js";

const router = Router();

router.post("/presigned-url", ctrl.getPresignedUrl);  // { recordId, fileType }

export default router;
