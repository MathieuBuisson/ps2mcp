const testToolInputSchema = z.object({
  Count: z.number().int().min(1).max(100),
});
