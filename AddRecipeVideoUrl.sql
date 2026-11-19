BEGIN TRANSACTION;
ALTER TABLE [Recipes] ADD [VideoUrl] nvarchar(500) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261118204118_AddRecipeVideoUrl', N'10.0.14');

COMMIT;
GO

