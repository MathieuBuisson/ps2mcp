const testToolInputSchema = z.object({
  Mode: z.enum(["Alpha", "Beta"]).refine((value) => new RegExp("^[A-Z][a-z]+$").test(value), { message: "Expected value matching pattern ^[A-Z][a-z]+$." }).optional(),
});
