--============================================= Drops

/****** Object:  Table [dbo].[TaskEvents] ******/
IF  EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[TaskEvents]') AND type in (N'U'))
DROP TABLE [dbo].[TaskEvents]
GO

/****** Object:  Table [dbo].[Tasks] ******/
IF  EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Tasks]') AND type in (N'U'))
DROP TABLE [dbo].[Tasks]
GO

/****** Object:  Index [IDX_AppId]    Script Date: 3/5/2015 11:42:11 AM ******/
IF  EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[Applications]') AND name = N'IDX_AppId')
DROP INDEX [IDX_AppId] ON [dbo].[Applications]
GO

/****** Object:  Table [dbo].[Applications]    Script Date: 3/5/2015 11:42:11 AM ******/
IF  EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Applications]') AND type in (N'U'))
DROP TABLE [dbo].[Applications]
GO

--============================================= Install

/****** Object:  Table [dbo].[Applications]    Script Date: 3/5/2015 11:51:33 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Applications]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[Applications](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[AppId] [nvarchar](50) NOT NULL,
	[AppData] [nvarchar](max) NULL,
	[RegistrationDate] [datetime2](7) NOT NULL,
	[LastUpdateDate] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_Applications] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
END
GO

SET ANSI_PADDING ON

GO

/****** Object:  Index [IDX_AppId]    Script Date: 3/5/2015 11:51:33 AM ******/
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[Applications]') AND name = N'IDX_AppId')
CREATE UNIQUE NONCLUSTERED INDEX [IDX_AppId] ON [dbo].[Applications]
(
	[AppId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO


/****** Object:  Table [dbo].[Tasks] ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Tasks](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Type] [nvarchar](450) NOT NULL,
	[Title] [nvarchar](250) NOT NULL,
	[Order] [float] NOT NULL,
	[Tag] [nvarchar](450) NULL,
	[RegisteredAt] [datetime] NOT NULL,
	[AppId] [nvarchar](50) NULL,
	[FinalizeUrl] [nvarchar](max) NULL,
	[LastLockUpdate] [datetime] NULL,
	[LockedBy] [nvarchar](450) NULL,
	[Hash] [bigint] NOT NULL,
	[TaskData] [nvarchar](max) NULL,
 CONSTRAINT [PK_dbo.Tasks] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

CREATE NONCLUSTERED INDEX [IX_Tasks_Hash] ON [dbo].[Tasks] 
(
	[Hash] ASC 
) WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
GO

/****** Object:  Table [dbo].[TaskEvents] ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[TaskEvents](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[SubTaskId] [uniqueidentifier] NULL,
	[EventType] [nvarchar](50) NOT NULL,
	[EventTime] [datetime] NOT NULL,
	[Title] [nvarchar](250) NOT NULL,
	[Tag] [nvarchar](450) NULL,
	[Details] [nvarchar](max) NULL,
	[AppId] [nvarchar](50) NULL,
	[Machine] [nvarchar](50) NULL,
	[Agent] [nvarchar](50) NULL,
	[TaskId] [int] NULL,
	[TaskType] [nvarchar](450) NULL,
	[TaskOrder] [float] NULL,
	[TaskHash] [bigint] NULL,
	[TaskData] [nvarchar](max) NULL,
 CONSTRAINT [PK_TaskEvents] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

CREATE NONCLUSTERED INDEX [IX_TaskEvents_EventType_TaskId] ON [dbo].[TaskEvents] ([EventType],[EventTime]) INCLUDE ([TaskId])
GO

CREATE NONCLUSTERED INDEX [IX_TaskEvents_All] ON [dbo].[TaskEvents] ([EventType],[TaskId])
	INCLUDE ([Id],[SubTaskId],[EventTime],[Title],[Tag],[Details],[AppId],[Machine],[Agent],[TaskType],[TaskOrder],[TaskHash],[TaskData])
GO
