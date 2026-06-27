const testToolInputSchema = z.object({
  Email: z.string().regex(new RegExp("^.+@.+\\..+$")).optional(),
});
