BEGIN TRANSACTION;
CREATE TABLE [ShoppingListItems] (
    [Id] int NOT NULL IDENTITY,
    [UserId] nvarchar(450) NOT NULL,
    [WeekStart] date NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Done] bit NOT NULL,
    [AddedUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_ShoppingListItems] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ShoppingListItems_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_ShoppingListItems_UserId_WeekStart] ON [ShoppingListItems] ([UserId], [WeekStart]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261120175845_AddShoppingListItems', N'10.0.14');

COMMIT;
GO

