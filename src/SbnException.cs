using System;
using System.Runtime.Serialization;

namespace SbnIndex
{
    /// <summary>
    /// Exception for all <see cref="Sbn"/> related exceptions
    /// </summary>
    [Serializable]
    public class SbnException : Exception
    {
        /// <summary>
        /// Creates an instance of this class
        /// </summary>
        /// <param name="message">The message</param>
        public SbnException(string message)
            :base(message)
        {
        }

        /// <summary>
        /// Creates an instance of this class
        /// </summary>
        /// <param name="message">The exception message</param>
        /// <param name="innerException">The inner exception</param>
        public SbnException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Creates an instance of this class
        /// </summary>
        /// <param name="info">The serialization info</param>
        /// <param name="context">The streaming context</param>
        public SbnException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}