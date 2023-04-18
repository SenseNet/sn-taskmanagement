/****** Object:  Table [SignalR].[Messages_0]    Script Date: 6/17/2016 3:12:12 PM ******/
IF  EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[SignalR].[Messages_0]') AND type in (N'U'))
DROP TABLE [SignalR].[Messages_0]

/****** Object:  Table [SignalR].[Messages_0_Id]    Script Date: 6/17/2016 3:12:20 PM ******/
IF  EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[SignalR].[Messages_0_Id]') AND type in (N'U'))
DROP TABLE [SignalR].[Messages_0_Id]
GO

/****** Object:  Table [SignalR].[Schema]    Script Date: 6/17/2016 3:12:39 PM ******/
IF  EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[SignalR].[Schema]') AND type in (N'U'))
DROP TABLE [SignalR].[Schema]
GO

