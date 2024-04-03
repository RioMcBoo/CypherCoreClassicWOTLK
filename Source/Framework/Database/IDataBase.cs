namespace Framework.Database
{
    public interface IDataBase
    {
        public abstract SQLResult Query(PreparedStatement stmt);
    }
}
