const testToolInputSchema = z.object({
  Required: z.string(),
  Optional: z.string().optional(),
});
