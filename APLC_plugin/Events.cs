using System;
using System.Collections.ObjectModel;

namespace APLC;

public delegate void AplcEventHandler(object source, AplcEventArgs args);

public class AplcEventArgs : EventArgs
{
    public Collection<string> ReceivedItemNames;

    public AplcEventArgs(Collection<string> receivedItemNames)
    {
        ReceivedItemNames = receivedItemNames;
    }

    public Collection<string> GetReceivedItemNames()
    {
        return ReceivedItemNames;
    }
}