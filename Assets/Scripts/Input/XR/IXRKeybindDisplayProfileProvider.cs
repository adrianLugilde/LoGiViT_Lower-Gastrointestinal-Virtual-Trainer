using System;
using CustomUI;

public interface IXRKeybindDisplayProfileProvider
{
    event Action<XRKeybindDisplayProfile> OnKeybindDisplayProfileReady;
}