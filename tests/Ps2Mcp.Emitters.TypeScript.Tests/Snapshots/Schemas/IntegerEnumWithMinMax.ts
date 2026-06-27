const testToolInputSchema = z.object({
  Priority: z.union([z.literal(1), z.literal(2), z.literal(3)]).optional(),
});
