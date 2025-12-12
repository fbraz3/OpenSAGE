using System;

namespace OpenSage.Graphics.Core;

/// <summary>
/// Graphics exception for errors that occur during graphics operations.
/// </summary>
public class GraphicsException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GraphicsException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public GraphicsException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphicsException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public GraphicsException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
