using Ayso.Shared.Domain.Interfaces;

namespace Ayso.Shared.Storage;

internal sealed class StorageWriteLockHandle : IStorageWriteLockHandle
{
	private readonly Action _release;
	private readonly Func<Task> _releaseAsync;
	private int _released;

	public StorageWriteLockHandle(string lockName, Action release, Func<Task> releaseAsync)
	{
		LockName = lockName;
		_release = release;
		_releaseAsync = releaseAsync;
	}

	public string LockName { get; }

	public void Dispose()
	{
		if (Interlocked.CompareExchange(ref _released, 1, 0) != 0)
		{
			return;
		}

		_release();
	}

	public async ValueTask DisposeAsync()
	{
		if (Interlocked.CompareExchange(ref _released, 1, 0) != 0)
		{
			return;
		}

		await _releaseAsync();
	}
}
