namespace MultiTenant
{
    public interface IMyRandomService
    {
        string GetData();
    }
    public class MyRandomService : IMyRandomService
    {
        private string _data;
        public MyRandomService()
        {
            _data = Guid.NewGuid().ToString();
        }
        public string GetData()
        {
            return _data;
        }
    }
}
