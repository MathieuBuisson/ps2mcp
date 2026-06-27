const testToolInputSchema = z.object({
  Config: z.object({
    Id: z.number().int(),
  }),
});
