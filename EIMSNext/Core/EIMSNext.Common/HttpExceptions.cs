using HKH.Common;

namespace EIMSNext.Common
{
    /// <summary>
    /// 表示与 HTTP 状态码相关联的异常基类，用于在业务逻辑中抛出可映射为特定 HTTP 响应的错误。
    /// </summary>
    public abstract class HttpException : UnLogException
    {
        /// <summary>
        /// 使用指定的错误消息初始化 <see cref="HttpException"/> 类的新实例。
        /// </summary>
        /// <param name="message">描述错误的消息。</param>
        protected HttpException(string message) : base(message)
        {
        }

        /// <summary>
        /// 使用指定的错误消息和内部异常初始化 <see cref="HttpException"/> 类的新实例。
        /// </summary>
        /// <param name="message">描述错误的消息。</param>
        /// <param name="innerException">导致当前异常的内部异常。</param>
        protected HttpException(string message, Exception innerException) : base(message, innerException)
        {
        }

        /// <summary>
        /// 获取此异常对应的 HTTP 状态码。
        /// </summary>
        public abstract int StatusCode { get; }

        /// <summary>
        /// 获取此异常对应的业务状态码字符串。
        /// </summary>
        public abstract string StateCode { get; }
    }

    /// <summary>
    /// 表示 HTTP 400 Bad Request 错误的异常。
    /// </summary>
    public sealed class BadRequestException : HttpException
    {
        /// <summary>
        /// 使用指定的错误消息初始化 <see cref="BadRequestException"/> 类的新实例。
        /// </summary>
        /// <param name="message">描述错误的消息。</param>
        public BadRequestException(string message) : base(message)
        {
        }

        /// <summary>
        /// 使用指定的错误消息和内部异常初始化 <see cref="BadRequestException"/> 类的新实例。
        /// </summary>
        /// <param name="message">描述错误的消息。</param>
        /// <param name="innerException">导致当前异常的内部异常。</param>
        public BadRequestException(string message, Exception innerException) : base(message, innerException)
        {
        }

        /// <summary>
        /// 获取 HTTP 状态码 400。
        /// </summary>
        public override int StatusCode => 400;

        /// <summary>
        /// 获取业务状态码 "badrequest"。
        /// </summary>
        public override string StateCode => "badrequest";
    }

    /// <summary>
    /// 表示 HTTP 401 Unauthorized 错误的异常。
    /// </summary>
    public sealed class UnauthorizedException : HttpException
    {
        /// <summary>
        /// 使用指定的错误消息初始化 <see cref="UnauthorizedException"/> 类的新实例。
        /// </summary>
        /// <param name="message">描述错误的消息。</param>
        public UnauthorizedException(string message) : base(message)
        {
        }

        /// <summary>
        /// 使用指定的错误消息和内部异常初始化 <see cref="UnauthorizedException"/> 类的新实例。
        /// </summary>
        /// <param name="message">描述错误的消息。</param>
        /// <param name="innerException">导致当前异常的内部异常。</param>
        public UnauthorizedException(string message, Exception innerException) : base(message, innerException)
        {
        }

        /// <summary>
        /// 获取 HTTP 状态码 401。
        /// </summary>
        public override int StatusCode => 401;

        /// <summary>
        /// 获取业务状态码 "unauthorized"。
        /// </summary>
        public override string StateCode => "unauthorized";
    }

    /// <summary>
    /// 表示 HTTP 403 Forbidden 错误的异常。
    /// </summary>
    public sealed class ForbiddenException : HttpException
    {
        /// <summary>
        /// 使用指定的错误消息初始化 <see cref="ForbiddenException"/> 类的新实例。
        /// </summary>
        /// <param name="message">描述错误的消息。</param>
        public ForbiddenException(string message) : base(message)
        {
        }

        /// <summary>
        /// 使用指定的错误消息和内部异常初始化 <see cref="ForbiddenException"/> 类的新实例。
        /// </summary>
        /// <param name="message">描述错误的消息。</param>
        /// <param name="innerException">导致当前异常的内部异常。</param>
        public ForbiddenException(string message, Exception innerException) : base(message, innerException)
        {
        }

        /// <summary>
        /// 获取 HTTP 状态码 403。
        /// </summary>
        public override int StatusCode => 403;

        /// <summary>
        /// 获取业务状态码 "forbidden"。
        /// </summary>
        public override string StateCode => "forbidden";
    }

    /// <summary>
    /// 表示 HTTP 404 Not Found 错误的异常。
    /// </summary>
    public sealed class NotFoundException : HttpException
    {
        /// <summary>
        /// 使用指定的错误消息初始化 <see cref="NotFoundException"/> 类的新实例。
        /// </summary>
        /// <param name="message">描述错误的消息。</param>
        public NotFoundException(string message) : base(message)
        {
        }

        /// <summary>
        /// 使用指定的错误消息和内部异常初始化 <see cref="NotFoundException"/> 类的新实例。
        /// </summary>
        /// <param name="message">描述错误的消息。</param>
        /// <param name="innerException">导致当前异常的内部异常。</param>
        public NotFoundException(string message, Exception innerException) : base(message, innerException)
        {
        }

        /// <summary>
        /// 获取 HTTP 状态码 404。
        /// </summary>
        public override int StatusCode => 404;

        /// <summary>
        /// 获取业务状态码 "notfound"。
        /// </summary>
        public override string StateCode => "notfound";
    }

    /// <summary>
    /// 表示 HTTP 409 Conflict 错误的异常。
    /// </summary>
    public sealed class ConflictException : HttpException
    {
        /// <summary>
        /// 使用指定的错误消息初始化 <see cref="ConflictException"/> 类的新实例。
        /// </summary>
        /// <param name="message">描述错误的消息。</param>
        public ConflictException(string message) : base(message)
        {
        }

        /// <summary>
        /// 使用指定的错误消息和内部异常初始化 <see cref="ConflictException"/> 类的新实例。
        /// </summary>
        /// <param name="message">描述错误的消息。</param>
        /// <param name="innerException">导致当前异常的内部异常。</param>
        public ConflictException(string message, Exception innerException) : base(message, innerException)
        {
        }

        /// <summary>
        /// 获取 HTTP 状态码 409。
        /// </summary>
        public override int StatusCode => 409;

        /// <summary>
        /// 获取业务状态码 "conflict"。
        /// </summary>
        public override string StateCode => "conflict";
    }

    /// <summary>
    /// 表示 HTTP 422 Unprocessable Entity 错误的异常。
    /// </summary>
    public sealed class UnprocessableEntityException : HttpException
    {
        /// <summary>
        /// 使用指定的错误消息初始化 <see cref="UnprocessableEntityException"/> 类的新实例。
        /// </summary>
        /// <param name="message">描述错误的消息。</param>
        public UnprocessableEntityException(string message) : base(message)
        {
        }

        /// <summary>
        /// 使用指定的错误消息和内部异常初始化 <see cref="UnprocessableEntityException"/> 类的新实例。
        /// </summary>
        /// <param name="message">描述错误的消息。</param>
        /// <param name="innerException">导致当前异常的内部异常。</param>
        public UnprocessableEntityException(string message, Exception innerException) : base(message, innerException)
        {
        }

        /// <summary>
        /// 获取 HTTP 状态码 422。
        /// </summary>
        public override int StatusCode => 422;

        /// <summary>
        /// 获取业务状态码 "unprocessableentity"。
        /// </summary>
        public override string StateCode => "unprocessableentity";
    }

    /// <summary>
    /// 表示 HTTP 500 Internal Server Error 错误的异常。
    /// </summary>
    public sealed class InternalServerException : HttpException
    {
        /// <summary>
        /// 使用指定的错误消息初始化 <see cref="InternalServerException"/> 类的新实例。
        /// </summary>
        /// <param name="message">描述错误的消息。</param>
        public InternalServerException(string message) : base(message)
        {
        }

        /// <summary>
        /// 使用指定的错误消息和内部异常初始化 <see cref="InternalServerException"/> 类的新实例。
        /// </summary>
        /// <param name="message">描述错误的消息。</param>
        /// <param name="innerException">导致当前异常的内部异常。</param>
        public InternalServerException(string message, Exception innerException) : base(message, innerException)
        {
        }

        /// <summary>
        /// 获取 HTTP 状态码 500。
        /// </summary>
        public override int StatusCode => 500;

        /// <summary>
        /// 获取业务状态码 "internalservererror"。
        /// </summary>
        public override string StateCode => "internalservererror";
    }
}
