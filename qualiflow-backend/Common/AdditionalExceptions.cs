namespace DocApi.Common
{
    public class UnauthorizedException : ServiceException
    {
        public UnauthorizedException(string message) : base(message) { }
    }

    public class ForbiddenException : ServiceException
    {
        public ForbiddenException(string message) : base(message) { }
    }

}
