const testToolInputSchema = z.object({
  Score: z.union([z.literal(1.5), z.literal(2.5)]).optional(),
});
