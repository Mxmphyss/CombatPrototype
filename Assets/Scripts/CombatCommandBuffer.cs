using UnityEngine;

public sealed class CombatCommandBuffer
{
    private readonly int lifetimeFrames;
    private CombatFrameCommand command;
    private bool hasCommand;

    public CombatCommandBufferStatus LastStatus { get; private set; }
    public bool HasCommand => hasCommand;
    public CombatActionId BufferedAction =>
        hasCommand ? command.ActionId : CombatActionId.None;
    public CombatFrameCommand Command => command;

    public CombatCommandBuffer(int lifetimeFrames)
    {
        this.lifetimeFrames = Mathf.Max(1, lifetimeFrames);
    }

    public CombatCommandBufferStatus Store(
        CombatFrameCommand nextCommand)
    {
        if (!nextCommand.IsValid)
        {
            LastStatus = CombatCommandBufferStatus.Rejected;
            return LastStatus;
        }

        LastStatus = hasCommand
            ? CombatCommandBufferStatus.Replaced
            : CombatCommandBufferStatus.Buffered;
        command = nextCommand;
        hasCommand = true;
        return LastStatus;
    }

    public bool TryConsume(
        int currentFrame,
        out CombatFrameCommand consumed)
    {
        if (!hasCommand)
        {
            consumed = default;
            return false;
        }

        if (IsExpired(currentFrame))
        {
            Expire();
            consumed = default;
            return false;
        }

        consumed = command;
        hasCommand = false;
        command = default;
        LastStatus = CombatCommandBufferStatus.Started;
        return true;
    }

    public bool UpdateExpiration(int currentFrame)
    {
        if (!hasCommand || !IsExpired(currentFrame))
            return false;

        Expire();
        return true;
    }

    public int RemainingFrames(int currentFrame)
    {
        if (!hasCommand)
            return 0;

        int age = Mathf.Max(0, currentFrame - command.SubmittedFrame);
        return Mathf.Max(0, lifetimeFrames - age);
    }

    public void Clear(
        CombatCommandBufferStatus status =
            CombatCommandBufferStatus.None)
    {
        hasCommand = false;
        command = default;
        LastStatus = status;
    }

    private bool IsExpired(int currentFrame)
    {
        return currentFrame - command.SubmittedFrame >=
               lifetimeFrames;
    }

    private void Expire()
    {
        hasCommand = false;
        command = default;
        LastStatus = CombatCommandBufferStatus.Expired;
    }
}
