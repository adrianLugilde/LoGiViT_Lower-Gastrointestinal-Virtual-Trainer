

using System;

public interface IXRDesktopModeProvider
{
    event Action<bool> OnDesktopModeChange;
}