BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122153745_AddMealPlanEntryMeal'
)
BEGIN
    ALTER TABLE [MealPlanEntries] ADD [Meal] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122153745_AddMealPlanEntryMeal'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261122153745_AddMealPlanEntryMeal', N'10.0.14');
END;

COMMIT;
GO

