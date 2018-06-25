using System;
using System.Collections.Generic;
using System.IO;
using SpringHeroBank.entity;

namespace SpringHeroBank.utility
{
    public class FileHandle
    {
        public static List<Transaction> ReadTransactions()
        {
            var list = new List<Transaction>();
            var lines = File.ReadAllLines("NeverEverGetBackTogether.txt");
            for (var i = 0; i < lines.Length; i += 1)
            {
                if (i == 0)
                {
                    continue;
                }

                var linesSplited = lines[i].Split("|");
                if (linesSplited.Length == 8)
                {
                    var tx = new Transaction()
                    {
                        Id = linesSplited[0],
                        SenderAccountNumber = linesSplited[1],
                        ReceiverAccountNumber = linesSplited[2],
                        Type = (Transaction.TransactionType) Int32.Parse(linesSplited[3]),
                        Amount = Decimal.Parse(linesSplited[4]),
                        Content = linesSplited[5],
                        CreatedAt = linesSplited[6],
                        Status = (Transaction.ActiveStatus) Int32.Parse(linesSplited[7])
                    };
                    list.Add(tx);
                }
            }

            return list;
        }

        public static Dictionary<string, Account> ReadAccounts()
        {
            var dictionary = new Dictionary<string, Account>();
            var lines = File.ReadAllLines("ForgetMeNot.txt");
            for (var i = 0; i < lines.Length; i += 1)
            {
                if (i == 0)
                {
                    continue;
                }

                var linesSplited = lines[i].Split("|");
                if (linesSplited.Length == 6)
                {
                    var acc = new Account()
                    {
                        AccountNumber = linesSplited[0],
                        Username = linesSplited[1],
                        FullName = linesSplited[2],
                        Balance = Decimal.Parse(linesSplited[3]),
                        Salt = linesSplited[4],
                        Status = (Account.ActiveStatus) Int32.Parse(linesSplited[5])
                    };
                    if (dictionary.ContainsKey(acc.AccountNumber))
                    {
                        continue;
                    }

                    dictionary.Add(acc.AccountNumber, acc);
                }
            }

            return dictionary;
        }
    }
}