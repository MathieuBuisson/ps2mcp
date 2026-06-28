// Snapshot artifact: auto-generated test fixture. Not runtime source.
const testToolInputSchema = z.object({
  Email: z.string().regex(new RegExp("^.+@.+\\..+$")).optional(),
});
