CREATE DATABASE [TransferDb] 
GO

USE [TransferDb]
GO

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
GO

CREATE TABLE [TransferLogs] (
    [Id] int NOT NULL IDENTITY,
    [FromAccount] int NOT NULL,
    [ToAccount] int NOT NULL,
    [TransferAmount] decimal(18,2) NOT NULL,
    CONSTRAINT [PK_TransferLogs] PRIMARY KEY ([Id])
);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20221104115153_InitialCreate', N'6.0.10');
GO

COMMIT;
GO

