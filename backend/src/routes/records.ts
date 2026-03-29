import { Router } from "express";
import * as ctrl from "../controllers/records.controller.js";

const router = Router();

router.get("/", ctrl.list);                  // ?state=&professional_id=
router.get("/:id", ctrl.getById);
router.post("/", ctrl.create);
router.put("/:id", ctrl.update);
router.patch("/:id/state", ctrl.transitionState);  // { state, error_reason? }
router.delete("/:id", ctrl.remove);

export default router;
