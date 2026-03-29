import { Router } from "express";
import * as ctrl from "../controllers/professionals.controller.js";

const router = Router();

router.get("/", ctrl.list);           // ?company_id= for filtering
router.get("/:id", ctrl.getById);
router.post("/", ctrl.create);
router.put("/:id", ctrl.update);
router.delete("/:id", ctrl.remove);

export default router;
