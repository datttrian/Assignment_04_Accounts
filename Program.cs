﻿public struct Transaction
{
    public string AccountNumber { get; }
    public double Amount { get; }
    public Person Originator { get; }
    public DayTime Time { get; }

    public Transaction(string accountNumber, double amount, Person person)
    {
        AccountNumber = accountNumber;
        Amount = amount;
        Originator = person;
        Time = Utils.Time;
    }

    public override string ToString()
    {
        string transactionType = Amount >= 0 ? "Deposit" : "Withdraw";
        return $"{transactionType}: Account Number - {AccountNumber}, Person - {Originator.Name}, Amount - {Amount}, Time - {Time}";
    }
}

public struct DayTime
{
    private long minutes;

    public DayTime(long minutes)
    {
        this.minutes = minutes;
    }

    public static DayTime operator +(DayTime lhs, int minutes)
    {
        return new DayTime(lhs.minutes + minutes);
    }

    public override string ToString()
    {
        long year = minutes / (30 * 24 * 60 * 12);
        long month = (minutes % (30 * 24 * 60 * 12)) / (30 * 24 * 60);
        long day = ((minutes % (30 * 24 * 60 * 12)) % (30 * 24 * 60)) / (24 * 60);
        long hour = (((minutes % (30 * 24 * 60 * 12)) % (30 * 24 * 60)) % (24 * 60)) / 60;
        long remainingMinutes = (((minutes % (30 * 24 * 60 * 12)) % (30 * 24 * 60)) % (24 * 60)) % 60;
        return $"{year}-{month}-{day} {hour}:{remainingMinutes}";
    }
}

public interface ITransaction
{
    void Withdraw(double amount, Person person);
    void Deposit(double amount, Person person);
}

public enum ExceptionType
{
    ACCOUNT_DOES_NOT_EXIST,
    CREDIT_LIMIT_HAS_BEEN_EXCEEDED,
    NAME_NOT_ASSOCIATED_WITH_ACCOUNT,
    NO_OVERDRAFT,
    PASSWORD_INCORRECT,
    USER_DOES_NOT_EXIST,
    USER_NOT_LOGGED_IN
}

public enum AccountType
{
    Checking,
    Saving,
    Visa
}

//this class depends of the implementation of the following types:
//DayTime struct and AccountType enum
public static class Utils
{
    static DayTime _time = new DayTime(1_048_000_000);
    static Random random = new Random();
    public static DayTime Time
    {
        get => _time += random.Next(1000);
    }
    public static DayTime Now
    {
        get => _time += 0;
    }

    public readonly static Dictionary<AccountType, string> ACCOUNT_TYPES =
        new Dictionary<AccountType, string>
    {
        { AccountType.Checking , "CK" },
        { AccountType.Saving , "SV" },
        { AccountType.Visa , "VS" }
    };

}

public class Person
{
    private string password;
    public event EventHandler<LoginEventArgs> OnLogin;

    public string SIN { get; }
    public string Name { get; }
    public bool IsAuthenticated { get; private set; }

    public Person(string name, string sin)
    {
        Name = name;
        SIN = sin;
        password = SIN.Substring(0, 3);
        IsAuthenticated = false;
    }

    public void Login(string password)
    {
        if (password != this.password)
        {
            IsAuthenticated = false;
            OnLogin?.Invoke(this, new LoginEventArgs(Name, false));
            throw new AccountException(ExceptionType.PASSWORD_INCORRECT);
        }
        else
        {
            IsAuthenticated = true;
            OnLogin?.Invoke(this, new LoginEventArgs(Name, true));
        }
    }

    public void Logout()
    {
        IsAuthenticated = false;
    }

    public override string ToString()
    {
        return Name;
    }
}

public class LoginEventArgs : EventArgs
{
    public string Name { get; }
    public bool Success { get; }

    public LoginEventArgs(string name, bool success)
    {
        Name = name;
        Success = success;
    }
}

public class AccountException : Exception
{
    public ExceptionType Type { get; }

    public AccountException(ExceptionType type)
    {
        Type = type;
    }
}

public class TransactionEventArgs : LoginEventArgs
{
    public double Amount { get; }

    public TransactionEventArgs(string personName, double amount, bool success) : base(personName, success)
    {
        Amount = amount;
    }
}

public static class Logger
{
    private static List<string> loginEvents = new List<string>();
    private static List<string> transactionEvents = new List<string>();

    public static void LoginHandler(object sender, EventArgs args)
    {
        LoginEventArgs loginArgs = args as LoginEventArgs;
        string logMessage = $"{loginArgs.Name} login attempt: {loginArgs.Success}, Time: {Utils.Now}";
        loginEvents.Add(logMessage);
    }

    public static void TransactionHandler(object sender, EventArgs args)
    {
        TransactionEventArgs transactionArgs = args as TransactionEventArgs;
        string logMessage = $"{transactionArgs.Name} {transactionArgs.Success} transaction: {transactionArgs.Amount}, Time: {Utils.Now}";
        transactionEvents.Add(logMessage);
    }

    public static void ShowLoginEvents()
    {
        Console.WriteLine($"Current Time: {Utils.Now}");
        Console.WriteLine("Login Events:");
        for (int i = 0; i < loginEvents.Count; i++)
        {
            Console.WriteLine($"{i + 1}. {loginEvents[i]}");
        }
    }

    public static void ShowTransactionEvents()
    {
        Console.WriteLine($"Current Time: {Utils.Now}");
        Console.WriteLine("Transaction Events:");
        for (int i = 0; i < transactionEvents.Count; i++)
        {
            Console.WriteLine($"{i + 1}. {transactionEvents[i]}");
        }
    }
}

public abstract class Account
{
    private static int LAST_NUMBER = 100_000;

    protected readonly List<Person> users;
    protected readonly List<Transaction> transactions;

    public event EventHandler<TransactionEventArgs> OnTransaction;

    public string Number { get; }
    public double Balance { get; protected set; }
    public double LowestBalance { get; protected set; }

    public Account(string type, double balance)
    {
        Number = $"{type}-{++LAST_NUMBER}";
        Balance = balance;
        LowestBalance = balance;
        transactions = new List<Transaction>();
        users = new List<Person>();
    }

    public void Deposit(double amount, Person person)
    {
        Balance += amount;
        if (Balance < LowestBalance)
            LowestBalance = Balance;

        Transaction transaction = new Transaction(Number, amount, person);
        transactions.Add(transaction);

        OnTransaction?.Invoke(this, new TransactionEventArgs(person.Name, amount, true));
    }

    public void AddUser(Person person)
    {
        users.Add(person);
    }

    public bool IsUser(string name)
    {
        return users.Any(user => user.Name == name);
    }

    public abstract void PrepareMonthlyStatement();

    protected void RaiseOnTransaction(TransactionEventArgs args)
    {
        OnTransaction?.Invoke(this, args);
    }

    public override string ToString()
    {
        string transactionDetails = string.Join("\n", transactions.Select((transaction, index) => $"{index + 1}. {transaction}"));
        string userNames = string.Join(", ", users.Select(user => user.Name));

        return $"Account Number: {Number}\nUsers: {userNames}\nBalance: {Balance}\nTransactions:\n{transactionDetails}";
    }
}

public class CheckingAccount : Account, ITransaction
{
    // Class variables
    public static readonly double COST_PER_TRANSACTION = 0.05;
    public static readonly double INTEREST_RATE = 0.005;

    // Instance variables
    private bool hasOverdraft;

    // Constructor
    public CheckingAccount(double balance = 0, bool hasOverdraft = false)
        : base(Utils.ACCOUNT_TYPES[AccountType.Checking], balance)
    {
        this.hasOverdraft = hasOverdraft;
    }

    // Override Deposit method to include transaction cost and event
    public new void Deposit(double amount, Person person)
    {
        base.Deposit(amount, person); // Call base class deposit method
                                      // In CheckingAccount methods where you want to raise the event
        RaiseOnTransaction(new TransactionEventArgs(person.Name, amount, true));

    }

    // Implement Withdraw method from ITransaction
    public void Withdraw(double amount, Person person)
    {
        if (!IsUser(person.Name))
        {
            // In CheckingAccount methods where you want to raise the event
            RaiseOnTransaction(new TransactionEventArgs(person.Name, amount, false));

            throw new AccountException(ExceptionType.NAME_NOT_ASSOCIATED_WITH_ACCOUNT);
        }
        else if (!person.IsAuthenticated)
        {
            // In CheckingAccount methods where you want to raise the event
            RaiseOnTransaction(new TransactionEventArgs(person.Name, amount, false));

            throw new AccountException(ExceptionType.USER_NOT_LOGGED_IN);
        }
        else if (amount > Balance && !hasOverdraft)
        {
            // In CheckingAccount methods where you want to raise the event
            RaiseOnTransaction(new TransactionEventArgs(person.Name, amount, false));

            throw new AccountException(ExceptionType.NO_OVERDRAFT);
        }
        else
        {
            // For withdrawal, deposit a negative amount
            base.Deposit(-amount, person); // This adjusts the balance
                                           // In CheckingAccount methods where you want to raise the event
            RaiseOnTransaction(new TransactionEventArgs(person.Name, -amount, true));

        }
    }

    // Override PrepareMonthlyReport to handle checking account specifics
    public override void PrepareMonthlyStatement()
    {
        double serviceCharge = transactions.Count * COST_PER_TRANSACTION;
        double interest = (LowestBalance * INTEREST_RATE) / 12;
        Balance += interest - serviceCharge;
        transactions.Clear(); // Clear transactions after updating balance
    }
}

public class SavingAccount : Account, ITransaction
{
    // Class variables
    public static readonly double COST_PER_TRANSACTION = 0.5;
    public static readonly double INTEREST_RATE = 0.015;

    // Constructor
    public SavingAccount(double balance = 0)
        : base(Utils.ACCOUNT_TYPES[AccountType.Saving], balance)
    {
        // Note: hasOverdraft parameter removed as per the fields description
    }

    // Override Deposit method to include transaction cost and event
    public new void Deposit(double amount, Person person)
    {
        base.Deposit(amount, person); // Call base class deposit method
        RaiseOnTransaction(new TransactionEventArgs(person.Name, amount, true)); // Assuming RaiseOnTransaction is implemented in Account
    }

    // Implement Withdraw method from ITransaction
    public void Withdraw(double amount, Person person)
    {
        if (!IsUser(person.Name))
        {
            RaiseOnTransaction(new TransactionEventArgs(person.Name, amount, false));
            throw new AccountException(ExceptionType.NAME_NOT_ASSOCIATED_WITH_ACCOUNT);
        }
        else if (!person.IsAuthenticated)
        {
            RaiseOnTransaction(new TransactionEventArgs(person.Name, amount, false));
            throw new AccountException(ExceptionType.USER_NOT_LOGGED_IN);
        }
        else if (amount > Balance) // No overdraft for SavingAccount
        {
            RaiseOnTransaction(new TransactionEventArgs(person.Name, amount, false));
            throw new AccountException(ExceptionType.NO_OVERDRAFT);
        }
        else
        {
            base.Deposit(-amount, person); // Adjust balance by depositing a negative amount
            RaiseOnTransaction(new TransactionEventArgs(person.Name, -amount, true));
        }
    }

    // Override PrepareMonthlyReport to handle saving account specifics
    public override void PrepareMonthlyStatement()
    {
        double serviceCharge = transactions.Count * COST_PER_TRANSACTION;
        double interest = (LowestBalance * INTEREST_RATE) / 12;
        Balance += interest - serviceCharge;
        transactions.Clear(); // Clear transactions after updating balance
    }
}
