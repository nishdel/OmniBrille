namespace OmniBrille.Core;

public enum InteractionSoundCue
{
    Hover,
    Select,
    Navigate,
    FolderEnter,
    FileOpen,
}

public interface IInteractionSoundService : IDisposable
{
    public bool Enabled { get; set; }

    public void Play(InteractionSoundCue cue);
}
