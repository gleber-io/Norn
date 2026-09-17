import { z } from "zod";
import { platformModeSchema } from "./common";

export const modeResponseSchema = z.object({ mode: platformModeSchema });
export type ModeResponse = z.infer<typeof modeResponseSchema>;
