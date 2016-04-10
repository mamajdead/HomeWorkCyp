using System;
using System.Windows;
using System.Windows.Controls;
using System.Collections.Concurrent;
using System.Threading;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Threading;

namespace WpfApplication1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private HomeWork.SpecialQueue<int> mQueue = null; //Queue
        private HomeWork.Executor sender = null; //Thread sender
        private HomeWork.Executor receiver = null; //Thread receiver

        public MainWindow()
        {
            InitializeComponent();
            Initialaze();
            listBox.ItemsSource = mQueue;
            Start();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            Stop();
            Console.WriteLine("Window_Closing");
        }

        private void button_Click(object sender, RoutedEventArgs e)
        {
            int newValue = int.Parse(textBox.Text);
            ListBoxItem item = new ListBoxItem();
            mQueue.Enqueue(newValue);
            labelSendedNumber.Content = newValue;
        }

        /*! \fn void ShowReceivedNumber(int value).
            \brief Callback for changing UI control.
        */
        private void ShowReceivedNumber(int value)
        {
            labelReceivedNumber.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal,
                                new Action(
                                delegate ()
                                {
                                    labelReceivedNumber.Content = value;
                                }
                                ));
        }

        /*! \fn void ShowSendedNumber(int value).
            \brief Callback for changing UI control.
        */
        private void ShowSendedNumber(int value)
        {
            labelSendedNumber.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal,
                                new Action(
                                delegate ()
                                {
                                    labelSendedNumber.Content = value;
                                }
                                ));
        }

        /*! \fn Initialaze().
            \brief Initialization of the threads and Queue.
        */
        private void Initialaze()
        {
            mQueue = new HomeWork.SpecialQueue<int>();
            sender = new HomeWork.Sender(mQueue);
            sender.showNumber += new HomeWork.Executor.ShowNumberDelegat(ShowSendedNumber);
            receiver = new HomeWork.Receiver(mQueue);
            receiver.showNumber += new HomeWork.Executor.ShowNumberDelegat(ShowReceivedNumber);
            mQueue.Enqueue(1);
            mQueue.Enqueue(1);
            mQueue.Enqueue(1);
        }

        /*! \fn void Start().
            \brief Start threads.
        */
        private void Start()
        {
            sender.Run();
            receiver.Run();
        }

        /*! \fn void Stop().
            \brief Stop threads.
        */
        private void Stop()
        {
            sender.Stop();
            receiver.Stop();
        }
    }

}
namespace HomeWork
{
    /*! \class SpecialQueue
        \brief Queue.
        inherit : ConcurrentQueue<T> (Thread safe)
        implement : INotifyCollectionChanged (for binding with UI control) 
    */
    public class SpecialQueue<T> : ConcurrentQueue<T>, INotifyCollectionChanged
    {
        public event NotifyCollectionChangedEventHandler CollectionChanged;

        /*! \fn T Dequeue()
            \brief remove (an item of data awaiting processing) from a queue of such items.
            \return item
        */
        public T Dequeue()
        {
            T item;
            if (base.TryDequeue(out item))
            {
                this.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item));
                Console.WriteLine("Receiving was successful for Value: " + item);
            }
            else
            {
                Console.WriteLine("Receiving was failed for Value: " + item);
            }
            return item;
        }

        /*! \fn T Dequeue()
            \brief add (an item of data awaiting processing) to a queue of such items.
            \param item new item.
        */
        public new void Enqueue(T item)
        {
            base.Enqueue(item);
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item));
        }

        /*! \fn void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
            \brief Handler for NotifyCollectionChangedEvent event.
            \param e an NotifyCollectionChangedEventArgs.
        */
        protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            NotifyCollectionChangedEventHandler CollectionChanged = this.CollectionChanged;
            if (CollectionChanged != null)
            {
                foreach (NotifyCollectionChangedEventHandler nh in CollectionChanged.GetInvocationList())
                {
                    DispatcherObject dispObj = nh.Target as DispatcherObject;
                    if (dispObj != null)
                    {
                        Dispatcher dispatcher = dispObj.Dispatcher;
                        if (dispatcher != null && !dispatcher.CheckAccess())
                        {
                            dispatcher.BeginInvoke(
                                (Action)(() => nh.Invoke(this,
                                    new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset))),
                                DispatcherPriority.DataBind);
                            continue;
                        }
                    }
                    nh.Invoke(this, e);
                }
            }

        }

    }

    /*! \class Executor
        \brief Abstract base class. Wraper for creation new thread.
    */
    public abstract class Executor
    {
        public delegate void ShowNumberDelegat(int value); //Delegat for comunication with UI controls.
        public event ShowNumberDelegat showNumber; //Function delegat for comunication with UI controls.
        protected const int SLEEPING_TIME = 500; // Constant value for pause in thread execution.
        protected SpecialQueue<int> _Queue; // Queue
        protected bool _Stop = false; // Flag for stoping thread execution.
        private Thread _Thread = null; // Working thread.
        protected int _NewValue = 0; //Current int value

        /*! \fn T Executor(CircleQueue<int> queue)
            \brief Constructor.
            \param queue an CircleQueue.  
        */
        public Executor(SpecialQueue<int> queue)
        {
            _Queue = queue;
            _Thread = new Thread(doJob);
        }

        /*! \fn void Run().
            \brief Start thread execution.
        */
        public void Run()
        {
            _Stop = false;
            _Thread.Start();
        }

        /*! \fn void Stop().
            \brief Stop thread execution.
        */
        public void Stop()
        {
            _Stop = true;
        }

        /*! \fn void ShowNumber().
            \brief Function for run callback from UI in inherited class.
        */
        protected void ShowNumber()
        {
            showNumber(_NewValue);
        }

        /*! \fn void doJob().
            \brief Abstract function. Callback for thread.
            This method must be implemented in child's class. 
        */
        protected abstract void doJob();

    }

    public class Sender : Executor
    {
        /*! \var _ValueGenerator Random
            \brief Random generator.
        */
        private Random _ValueGenerator = new Random();

        /*! \fn T Sender(CircleQueue<int> queue)
            \brief Constructor.
            \param queue an CircleQueue.  
        */
        public Sender(SpecialQueue<int> queue)
            : base(queue)
        {
            Console.WriteLine("Constract Sender");
        }

        /*! \fn int generateValue()
            \brief Generate random int value.
            \return int value.
        */
        private int generateValue()
        {
            return _ValueGenerator.Next(0, 100);
        }

        /*! \fn void doJob().
            \brief Implementation of the abstract function. Callback for thread.
            Send value in to the queue.
        */
        protected override void doJob()
        {
            while (!_Stop)
            {
                _NewValue = generateValue();
                _Queue.Enqueue(_NewValue);
                Console.WriteLine("Sending Value: " + _NewValue);
                ShowNumber();
                Thread.Sleep(SLEEPING_TIME);
            }

        }

    }

    public class Receiver : Executor
    {
        /*! \fn T Receiver(CircleQueue<int> queue)
            \brief Constructor.
            \param queue an CircleQueue.  
        */
        public Receiver(SpecialQueue<int> queue)
            : base(queue)
        {
            Console.WriteLine("Constract Receiver");
        }

        /*! \fn void doJob().
            \brief Implementation of the abstract function. Callback for thread.
            Receive value from the queue.
        */
        protected override void doJob()
        {
            while (!_Stop)
            {
                if (_Queue.IsEmpty)
                {
                    Console.WriteLine("Resived Queue Is Empty");
                }
                else
                {
                    _NewValue = _Queue.Dequeue();
                    ShowNumber();
                }
                Thread.Sleep(SLEEPING_TIME);
            }
        }
    }
}


