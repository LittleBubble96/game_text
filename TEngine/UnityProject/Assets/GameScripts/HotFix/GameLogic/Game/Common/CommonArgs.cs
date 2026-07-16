namespace GameLogic
{
    public class CommonArgs
    {
        public static CommonArgs<T> Create<T>(T arg1)
        {
            return new CommonArgs<T>() { Arg1 = arg1 };
        }
        
        public static CommonArgs<T,T1> Create<T,T1>(T arg1, T1 arg2)
        {
            return new CommonArgs<T,T1>() { Arg1 = arg1, Arg2 = arg2 };
        }
        
        public static CommonArgs<T,T1,T2> Create<T,T1,T2>(T arg1, T1 arg2, T2 arg3)
        {
            return new CommonArgs<T,T1,T2>() { Arg1 = arg1, Arg2 = arg2, Arg3 = arg3 };
        }
        
        public static CommonArgs<T,T1,T2,T3> Create<T,T1,T2,T3>(T arg1, T1 arg2, T2 arg3, T3 arg4)
        {
            return new CommonArgs<T,T1,T2,T3>() { Arg1 = arg1, Arg2 = arg2, Arg3 = arg3, Arg4 = arg4 };
        }
    }

    public class CommonArgs<T> : CommonArgs
    {
        public T Arg1 { get; set; }
    }
    
    public class CommonArgs<T,T1> : CommonArgs<T>
    {
        public T1 Arg2 { get; set; }     
    }
    
    public class CommonArgs<T,T1,T2> : CommonArgs<T,T1>
    {
        public T2 Arg3 { get; set; }
    }   
    
    public class CommonArgs<T,T1,T2,T3> : CommonArgs<T,T1,T2>
    {
        public T3 Arg4 { get; set; }
    }
}