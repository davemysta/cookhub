BEGIN TRANSACTION;
ALTER TABLE [RecipeSteps] ADD [VideoSeconds] int NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261119170000_AddRecipeStepVideoSeconds', N'10.0.14');

COMMIT;
GO

