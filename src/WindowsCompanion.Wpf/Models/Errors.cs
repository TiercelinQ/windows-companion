namespace WindowsCompanion.Models;

/// <summary>A CIM/WMI query failed for a reason other than missing rights.</summary>
public sealed class CimQueryException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="CimQueryException"/> class.</summary>
    public CimQueryException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="CimQueryException"/> class.</summary>
    /// <param name="message">Technical message, logged only.</param>
    public CimQueryException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="CimQueryException"/> class.</summary>
    /// <param name="message">Technical message, logged only.</param>
    /// <param name="innerException">Underlying WMI or COM failure.</param>
    public CimQueryException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>A read or an action needs administrator rights the process does not have.</summary>
public sealed class ElevationRequiredException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="ElevationRequiredException"/> class.</summary>
    public ElevationRequiredException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ElevationRequiredException"/> class.</summary>
    /// <param name="message">Technical message, logged only.</param>
    public ElevationRequiredException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ElevationRequiredException"/> class.</summary>
    /// <param name="message">Technical message, logged only.</param>
    /// <param name="innerException">Underlying access-denied failure.</param>
    public ElevationRequiredException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>A JSON data file exists but cannot be read; it has been set aside as <c>.bak</c>.</summary>
public sealed class StoreCorruptedException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="StoreCorruptedException"/> class.</summary>
    public StoreCorruptedException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="StoreCorruptedException"/> class.</summary>
    /// <param name="message">Technical message, logged only.</param>
    public StoreCorruptedException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="StoreCorruptedException"/> class.</summary>
    /// <param name="message">Technical message, logged only.</param>
    /// <param name="innerException">Underlying parse or I/O failure.</param>
    public StoreCorruptedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
