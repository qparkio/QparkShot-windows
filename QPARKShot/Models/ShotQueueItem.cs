using System;

namespace QPARKShot.Models;

public sealed record ShotQueueItem(Guid Id, string Path, DateTime CapturedAt);
