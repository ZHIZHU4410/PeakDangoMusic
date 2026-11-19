using System;

namespace PeakDangoMusic;

internal sealed class MusicClient : IDisposable
{
	private readonly AggregatePlayer player;

	internal MusicState Snapshot => player.Snapshot;

	internal MusicClient(MusicSettings settings, AggregatePlayer aggregate)
	{
		player = aggregate;
	}

	internal void Configure(MusicSettings settings)
	{
	}

	internal void Reconnect()
	{
		player.Stop();
	}

	public void Dispose()
	{
	}
}
