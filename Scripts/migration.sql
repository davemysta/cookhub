IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
CREATE TABLE [AspNetRoles] (
    [Id] nvarchar(450) NOT NULL,
    [Name] nvarchar(256) NULL,
    [NormalizedName] nvarchar(256) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
);

CREATE TABLE [AspNetUsers] (
    [Id] nvarchar(450) NOT NULL,
    [DisplayName] nvarchar(100) NOT NULL,
    [UserName] nvarchar(256) NULL,
    [NormalizedUserName] nvarchar(256) NULL,
    [Email] nvarchar(256) NULL,
    [NormalizedEmail] nvarchar(256) NULL,
    [EmailConfirmed] bit NOT NULL,
    [PasswordHash] nvarchar(max) NULL,
    [SecurityStamp] nvarchar(max) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    [PhoneNumber] nvarchar(256) NULL,
    [PhoneNumberConfirmed] bit NOT NULL,
    [TwoFactorEnabled] bit NOT NULL,
    [LockoutEnd] datetimeoffset NULL,
    [LockoutEnabled] bit NOT NULL,
    [AccessFailedCount] int NOT NULL,
    CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
);

CREATE TABLE [Ingredients] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(100) NOT NULL,
    [NormalizedName] nvarchar(100) NOT NULL,
    CONSTRAINT [PK_Ingredients] PRIMARY KEY ([Id])
);

CREATE TABLE [Tags] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(50) NOT NULL,
    [NormalizedName] nvarchar(50) NOT NULL,
    CONSTRAINT [PK_Tags] PRIMARY KEY ([Id])
);

CREATE TABLE [AspNetRoleClaims] (
    [Id] int NOT NULL IDENTITY,
    [RoleId] nvarchar(450) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserClaims] (
    [Id] int NOT NULL IDENTITY,
    [UserId] nvarchar(450) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserLogins] (
    [LoginProvider] nvarchar(128) NOT NULL,
    [ProviderKey] nvarchar(128) NOT NULL,
    [ProviderDisplayName] nvarchar(max) NULL,
    [UserId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
    CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserPasskeys] (
    [CredentialId] varbinary(1024) NOT NULL,
    [UserId] nvarchar(450) NOT NULL,
    [Data] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_AspNetUserPasskeys] PRIMARY KEY ([CredentialId]),
    CONSTRAINT [FK_AspNetUserPasskeys_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserRoles] (
    [UserId] nvarchar(450) NOT NULL,
    [RoleId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
    CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserTokens] (
    [UserId] nvarchar(450) NOT NULL,
    [LoginProvider] nvarchar(128) NOT NULL,
    [Name] nvarchar(128) NOT NULL,
    [Value] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
    CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [FamilyMembers] (
    [Id] int NOT NULL IDENTITY,
    [UserId] nvarchar(450) NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    CONSTRAINT [PK_FamilyMembers] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_FamilyMembers_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Recipes] (
    [Id] int NOT NULL IDENTITY,
    [OwnerId] nvarchar(450) NOT NULL,
    [Title] nvarchar(200) NOT NULL,
    [Description] nvarchar(500) NULL,
    [LikedByOwner] bit NOT NULL,
    [CreatedUtc] datetime2 NOT NULL,
    [UpdatedUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_Recipes] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Recipes_AspNetUsers_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [IngredientsOnHand] (
    [UserId] nvarchar(450) NOT NULL,
    [IngredientId] int NOT NULL,
    [AddedUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_IngredientsOnHand] PRIMARY KEY ([UserId], [IngredientId]),
    CONSTRAINT [FK_IngredientsOnHand_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_IngredientsOnHand_Ingredients_IngredientId] FOREIGN KEY ([IngredientId]) REFERENCES [Ingredients] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [MealPlanEntries] (
    [Id] int NOT NULL IDENTITY,
    [RecipeId] int NOT NULL,
    [WeekStart] date NOT NULL,
    [Day] int NULL,
    [AddedUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_MealPlanEntries] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MealPlanEntries_Recipes_RecipeId] FOREIGN KEY ([RecipeId]) REFERENCES [Recipes] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [RecipeFamilyLikes] (
    [RecipeId] int NOT NULL,
    [FamilyMemberId] int NOT NULL,
    CONSTRAINT [PK_RecipeFamilyLikes] PRIMARY KEY ([RecipeId], [FamilyMemberId]),
    CONSTRAINT [FK_RecipeFamilyLikes_FamilyMembers_FamilyMemberId] FOREIGN KEY ([FamilyMemberId]) REFERENCES [FamilyMembers] ([Id]),
    CONSTRAINT [FK_RecipeFamilyLikes_Recipes_RecipeId] FOREIGN KEY ([RecipeId]) REFERENCES [Recipes] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [RecipeImages] (
    [RecipeId] int NOT NULL,
    [Data] varbinary(max) NOT NULL,
    [ContentType] nvarchar(100) NOT NULL,
    CONSTRAINT [PK_RecipeImages] PRIMARY KEY ([RecipeId]),
    CONSTRAINT [FK_RecipeImages_Recipes_RecipeId] FOREIGN KEY ([RecipeId]) REFERENCES [Recipes] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [RecipeIngredients] (
    [Id] int NOT NULL IDENTITY,
    [RecipeId] int NOT NULL,
    [IngredientId] int NOT NULL,
    [Amount] nvarchar(50) NOT NULL,
    [Preparation] nvarchar(100) NULL,
    [SortOrder] int NOT NULL,
    CONSTRAINT [PK_RecipeIngredients] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RecipeIngredients_Ingredients_IngredientId] FOREIGN KEY ([IngredientId]) REFERENCES [Ingredients] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_RecipeIngredients_Recipes_RecipeId] FOREIGN KEY ([RecipeId]) REFERENCES [Recipes] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [RecipeSteps] (
    [Id] int NOT NULL IDENTITY,
    [RecipeId] int NOT NULL,
    [StepNumber] int NOT NULL,
    [Instruction] nvarchar(2000) NOT NULL,
    CONSTRAINT [PK_RecipeSteps] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RecipeSteps_Recipes_RecipeId] FOREIGN KEY ([RecipeId]) REFERENCES [Recipes] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [RecipeTag] (
    [RecipesId] int NOT NULL,
    [TagsId] int NOT NULL,
    CONSTRAINT [PK_RecipeTag] PRIMARY KEY ([RecipesId], [TagsId]),
    CONSTRAINT [FK_RecipeTag_Recipes_RecipesId] FOREIGN KEY ([RecipesId]) REFERENCES [Recipes] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_RecipeTag_Tags_TagsId] FOREIGN KEY ([TagsId]) REFERENCES [Tags] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);

CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL;

CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);

CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);

CREATE INDEX [IX_AspNetUserPasskeys_UserId] ON [AspNetUserPasskeys] ([UserId]);

CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);

CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);

CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;

CREATE INDEX [IX_FamilyMembers_UserId] ON [FamilyMembers] ([UserId]);

CREATE UNIQUE INDEX [IX_Ingredients_NormalizedName] ON [Ingredients] ([NormalizedName]);

CREATE INDEX [IX_IngredientsOnHand_IngredientId] ON [IngredientsOnHand] ([IngredientId]);

CREATE INDEX [IX_MealPlanEntries_RecipeId] ON [MealPlanEntries] ([RecipeId]);

CREATE INDEX [IX_MealPlanEntries_WeekStart_RecipeId] ON [MealPlanEntries] ([WeekStart], [RecipeId]);

CREATE INDEX [IX_RecipeFamilyLikes_FamilyMemberId] ON [RecipeFamilyLikes] ([FamilyMemberId]);

CREATE INDEX [IX_RecipeIngredients_IngredientId] ON [RecipeIngredients] ([IngredientId]);

CREATE INDEX [IX_RecipeIngredients_RecipeId] ON [RecipeIngredients] ([RecipeId]);

CREATE INDEX [IX_Recipes_OwnerId_Title] ON [Recipes] ([OwnerId], [Title]);

CREATE INDEX [IX_RecipeSteps_RecipeId] ON [RecipeSteps] ([RecipeId]);

CREATE INDEX [IX_RecipeTag_TagsId] ON [RecipeTag] ([TagsId]);

CREATE UNIQUE INDEX [IX_Tags_NormalizedName] ON [Tags] ([NormalizedName]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261026154318_InitialCreate', N'10.0.14');

COMMIT;
GO

BEGIN TRANSACTION;
INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261026190740_InitialMigration', N'10.0.14');

COMMIT;
GO

BEGIN TRANSACTION;
DECLARE @var nvarchar(max);
SELECT @var = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[RecipeIngredients]') AND [c].[name] = N'Amount');
IF @var IS NOT NULL EXEC(N'ALTER TABLE [RecipeIngredients] DROP CONSTRAINT ' + @var + ';');
ALTER TABLE [RecipeIngredients] ALTER COLUMN [Amount] nvarchar(50) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261031155244_MakeIngredientAmountOptional', N'10.0.14');

COMMIT;
GO

