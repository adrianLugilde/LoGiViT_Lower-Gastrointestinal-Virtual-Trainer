using System;

public interface IXRViveDPadInteractor
{
    event Action OnDpadPressed;
    event Action OnDpadReleased;
}