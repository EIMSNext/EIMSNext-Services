using HKH.Common;

namespace EIMSNext.Common
{
    public abstract class HttpException : UnLogException
    {
        protected HttpException(string message) : base(message)
        {
        }

        protected HttpException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public abstract int StatusCode { get; }

        public abstract string StateCode { get; }
    }

    public sealed class BadRequestException : HttpException
    {
        public BadRequestException(string message) : base(message)
        {
        }

        public BadRequestException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public override int StatusCode => 400;

        public override string StateCode => "badrequest";
    }

    public sealed class UnauthorizedException : HttpException
    {
        public UnauthorizedException(string message) : base(message)
        {
        }

        public UnauthorizedException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public override int StatusCode => 401;

        public override string StateCode => "unauthorized";
    }

    public sealed class ForbiddenException : HttpException
    {
        public ForbiddenException(string message) : base(message)
        {
        }

        public ForbiddenException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public override int StatusCode => 403;

        public override string StateCode => "forbidden";
    }

    public sealed class NotFoundException : HttpException
    {
        public NotFoundException(string message) : base(message)
        {
        }

        public NotFoundException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public override int StatusCode => 404;

        public override string StateCode => "notfound";
    }

    public sealed class ConflictException : HttpException
    {
        public ConflictException(string message) : base(message)
        {
        }

        public ConflictException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public override int StatusCode => 409;

        public override string StateCode => "conflict";
    }

    public sealed class UnprocessableEntityException : HttpException
    {
        public UnprocessableEntityException(string message) : base(message)
        {
        }

        public UnprocessableEntityException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public override int StatusCode => 422;

        public override string StateCode => "unprocessableentity";
    }

    public sealed class InternalServerException : HttpException
    {
        public InternalServerException(string message) : base(message)
        {
        }

        public InternalServerException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public override int StatusCode => 500;

        public override string StateCode => "internalservererror";
    }
}
