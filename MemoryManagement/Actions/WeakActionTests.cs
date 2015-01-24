using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace MemoryManagement
{
    public class WeakActionTests
    {
        public class BasicClass : DisposeWatchable
        {
            private static HashSet<int> aliveIDs = new HashSet<int>();
            public static int curID = 0;

            public static bool IsAlive(int id)
            {
                return aliveIDs.Contains(id);
            }

            public readonly int ourID = curID++;

            public int value;
            public int nextValue;
            public BasicClass()
            {
                aliveIDs.Add(ourID);
            }
            public void setValue()
            {
                value = nextValue;
            }
            public void setValue(int arg)
            {
                value = arg;
            }
            ~BasicClass()
            {
                aliveIDs.Remove(ourID);
            }

            public override void Dispose()
            {
                base.Dispose();
            }
        }

        static int MakeWeakAction(out WeakAction<BasicClass> weakAction)
        {
            BasicClass basicClass = new BasicClass();
            int id = basicClass.ourID;
            Assert.IsTrue(BasicClass.IsAlive(id));
            basicClass.value = 0;
            basicClass.nextValue = 1;

            weakAction = new WeakAction<BasicClass>(basicClass.setValue);

            weakAction.Call();

            Assert.AreEqual(1, basicClass.value);
            return id;
        }
        [Test]
        public static void TestWeakAction()
        {
            WeakAction<BasicClass> weakAction;

            int id = MakeWeakAction(out weakAction);

            GCExts.ForceFullGC();
            Assert.IsFalse(weakAction.Call());
            //WeakAction doesn't store reference
            Assert.IsFalse(BasicClass.IsAlive(id));
        }

        static int MakeWeakAction1(out WeakAction1<BasicClass, int> weakAction)
        {
            BasicClass basicClass = new BasicClass();
            int id = basicClass.ourID;
            Assert.IsTrue(BasicClass.IsAlive(id));
            basicClass.value = 0;

            weakAction = new WeakAction1<BasicClass, int>(basicClass.setValue);

            weakAction.Call(1);

            Assert.AreEqual(1, basicClass.value);
            return id;
        }
        [Test]
        public static void TestWeakAction1()
        {
            WeakAction1<BasicClass, int> weakAction;

            int id = MakeWeakAction1(out weakAction);

            GCExts.ForceFullGC();
            Assert.IsFalse(weakAction.Call(1));
            //WeakAction doesn't store reference
            Assert.IsFalse(BasicClass.IsAlive(id));
        }

        static int MakeWeakActionNested(out IWeakAction weakAction)
        {
            BasicClass basicClass = new BasicClass();
            int id = basicClass.ourID;
            Assert.IsTrue(BasicClass.IsAlive(id));
            basicClass.value = 0;
            basicClass.nextValue = 1;

            weakAction = WeakHelper.MakeWeakAction(() =>
            {
                basicClass.setValue();
            });

            weakAction.Call();

            Assert.AreEqual(1, basicClass.value);
            return id;
        }
        [Test]
        public static void TestWeakActionNested()
        {
            IWeakAction weakAction;

            int id = MakeWeakActionNested(out weakAction);

            GCExts.ForceFullGC();
            Assert.IsFalse(weakAction.Call());
            //WeakAction doesn't store reference
            Assert.IsFalse(BasicClass.IsAlive(id));
        }

        static int MakeWeakAction1Nested(out IWeakAction1<int> weakAction)
        {
            BasicClass basicClass = new BasicClass();
            int id = basicClass.ourID;
            Assert.IsTrue(BasicClass.IsAlive(id)); 
            basicClass.value = 0;

            weakAction = WeakHelper.MakeWeakAction1<int>(x =>
            {
                basicClass.setValue(x);
            });

            weakAction.Call(1);

            Assert.AreEqual(1, basicClass.value);
            return id;
        }
        [Test]
        public static void TestWeakAction1Nested()
        {
            IWeakAction1<int> weakAction;

            int id = MakeWeakAction1Nested(out weakAction);

            GCExts.ForceFullGC();
            Assert.IsFalse(weakAction.Call(1));
            //WeakAction doesn't store reference
            Assert.IsFalse(BasicClass.IsAlive(id));
        }


        static int MakeAction(out Action action)
        {
            BasicClass basicClass = new BasicClass();
            int id = basicClass.ourID;
            Assert.IsTrue(BasicClass.IsAlive(id));

            basicClass.value = 0;
            basicClass.nextValue = 1;

            action = basicClass.setValue;
            action();

            Assert.AreEqual(1, basicClass.value);
            return id;
        }
        [Test]
        public static void TestAction()
        {
            Action action;

            int id = MakeAction(out action);

            GCExts.ForceFullGC();
            //Action stored reference to holder
            Assert.IsTrue(BasicClass.IsAlive(id));
        }

        [Test, TestCase(2.5)]
        public static void TestWeakActionSpeed(double weakFactorWorse)
        {
            int testCount = 10000;

            BasicClass instance = new BasicClass();

            WeakAction<BasicClass> weakAction 
                = new WeakAction<BasicClass>(instance.setValue);

            long weakTime = DateTime.Now.Ticks;
            for(int ix = 0; ix < testCount; ix++)
            {
                weakAction.Call();
            }
            weakTime = (DateTime.Now.Ticks - weakTime) / 10000;


            Action action = instance.setValue;

            long directTime = DateTime.Now.Ticks;
            for (int ix = 0; ix < testCount * weakFactorWorse; ix++)
            {
                action();
            }
            directTime = (DateTime.Now.Ticks - directTime) / 10000;

            Assert.Less(weakTime, directTime);
        }

        [Test, TestCase(30)]
        public static void TestWeakActionCtorSpeed(double weakFactorWorse)
        {
            int testCount = 1000;

            BasicClass instance = new BasicClass();
            long weakTime = DateTime.Now.Ticks;
            for (int ix = 0; ix < testCount; ix++)
            {
                WeakAction<BasicClass> weakAction = new WeakAction<BasicClass>(instance.setValue);
            }
            weakTime = DateTime.Now.Ticks - weakTime;


            long directTime = DateTime.Now.Ticks;
            for (int ix = 0; ix < testCount * weakFactorWorse; ix++)
            {
                Action action = instance.setValue;
            }
            directTime = DateTime.Now.Ticks - directTime;

            Assert.Less(weakTime, directTime);
        }

        [Test, TestCase(70)]
        public static void TestWeakActionCtorSpeedAnoymous(double weakFactorWorse)
        {
            int testCount = 1000;

            BasicClass instance = new BasicClass();
            long weakTime = DateTime.Now.Ticks;
            for (int ix = 0; ix < testCount; ix++)
            {
                IWeakAction weakAction = WeakHelper.MakeWeakAction(instance.setValue);
            }
            weakTime = (DateTime.Now.Ticks - weakTime) / 10000;


            long directTime = DateTime.Now.Ticks;
            for (int ix = 0; ix < testCount * weakFactorWorse; ix++)
            {
                Action action = instance.setValue;
            }
            directTime = (DateTime.Now.Ticks - directTime) / 10000;

            Assert.Less(weakTime, directTime);
        }
    }
}
