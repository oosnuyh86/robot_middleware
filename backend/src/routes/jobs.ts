import { Router } from "express";
import * as ctrl from "../controllers/jobs.controller.js";

const router = Router();

router.get("/", ctrl.list);            // ?company_id= for filtering
router.get("/:id", ctrl.getById);
router.post("/", ctrl.create);
router.put("/:id", ctrl.update);
router.patch("/:id/records", ctrl.addRecord);  // { record_id }
router.delete("/:id", ctrl.remove);

export default router;
