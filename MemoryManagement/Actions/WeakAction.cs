using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace MemoryManagement
{
    public interface IWeakAction
    {
        bool Call();
        bool IsAlive();
    }
    public class WeakAction<Context> : IWeakAction
        where Context : class
    {
        readonly WeakReference<Context> weakTarget;

        delegate void ActionDelegate(Context @this);
        readonly ActionDelegate method;

        public WeakAction(Action action)
        {
            this.weakTarget = new WeakReference<Context>((Context)action.Target);

            this.method = (ActionDelegate)Delegate.CreateDelegate(
                typeof(ActionDelegate), 
                action.Method);
        }

        public bool Call()
        {
            Context target;
            if(!weakTarget.TryGetTarget(out target))
            {
                return false;
            }

            method(target);

            return true;
        }
        public bool IsAlive()
        {
            Context target;
            return weakTarget.TryGetTarget(out target);
        }
    }

    public class StaticWeakAction : IWeakAction
    {
        readonly Action action;
        public StaticWeakAction(Action action)
        {
            if(action.Target != null)
            {
                throw new Exception("If the action has a Target, it is not static");
            }
            this.action = action;
        }
        public bool Call()
        {
            action();
            return true;
        }
        public bool IsAlive()
        {
            //The undead, nothing keeps us alive but we never have to die?
            //  This will probably cause leaks?
            return true;
        }
    }

    public interface IWeakAction1<Arg1>
    {
        bool Call(Arg1 arg);
        bool IsAlive();

        //Tries to get the original strong ref, if it is still alive
        bool TryGetStrongRef(out Action<Arg1> action);
    }

    //Hmm... this significantly hardens the interface and implementation...
    //  w/e, lets just say this makes it really fast or something :D
    public class WeakAction1<Context, Arg1> : IWeakAction1<Arg1>
        where Context : class
    {
        readonly WeakReference<Context> weakTarget;

        //Wait... there must have been a reason I didn't do this in the first place...
        readonly WeakReference<Action<Arg1>> weakAction;

        delegate void ActionDelegate(Context @this, Arg1 arg);
        readonly ActionDelegate method;

        public WeakAction1(Action<Arg1> action)
        {
            this.weakTarget = new WeakReference<Context>((Context)action.Target);
            this.weakAction = new WeakReference<Action<Arg1>>(action);

            this.method = (ActionDelegate)Delegate.CreateDelegate(
                typeof(ActionDelegate),
                action.Method);
        }

        public bool Call(Arg1 arg)
        {
            Context target;
            if (!weakTarget.TryGetTarget(out target))
            {
                return false;
            }

            method(target, arg);

            return true;
        }

        public bool IsAlive()
        {
            Context target;
            return weakTarget.TryGetTarget(out target);
        }

        public bool TryGetStrongRef(out Action<Arg1> action)
        {
            return weakAction.TryGetTarget(out action);
        }
    }

    public class StaticWeakAction1<T> : IWeakAction1<T>
    {
        readonly Action<T> action;
        public StaticWeakAction1(Action<T> action)
        {
            if (action.Target != null)
            {
                throw new Exception("If the action has a Target, it is not static");
            }
            this.action = action;
        }
        public bool Call(T arg)
        {
            action(arg);
            return true;
        }
        public bool IsAlive()
        {
            //The undead, nothing keeps us alive but we never have to die?
            //  This will probably cause leaks?
            return true;
        }

        public bool TryGetStrongRef(out Action<T> action)
        {
            action = this.action;
            return true;
        }
    }

    public static class WeakHelper
    {
        //Slower than the regular constructors, but requires less type arguments
        public static IWeakAction MakeWeakAction(Action action)
        {
            //static methods have no Target, but this actually makes them easier
            //  (and actually WeakActions by default!)
            if(action.Target == null)
            {
                return new StaticWeakAction(action);
            }

            Type contextType = action.Target.GetType();
            Type weakAction1Type = typeof(WeakAction<>);

            Type[] typeArgs = { contextType };
            Type newType = weakAction1Type.MakeGenericType(typeArgs);

            return (IWeakAction)Activator.CreateInstance(newType, action);
        }
        public static IWeakAction1<T> MakeWeakAction1<T>(Action<T> action)
        {
            if(action.Target == null)
            {
                return new StaticWeakAction1<T>(action);
            }

            Type contextType = action.Target.GetType();
            Type weakAction1Type = typeof(WeakAction1<,>);

            Type[] typeArgs = { contextType, typeof(T) };
            Type newType = weakAction1Type.MakeGenericType(typeArgs);

            return (IWeakAction1<T>)Activator.CreateInstance(newType, action);
        }
    }
}
