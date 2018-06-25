using System;
using ConsoleApp3.model;
using MySql.Data.MySqlClient;
using SpringHeroBank.entity;
using SpringHeroBank.error;
using SpringHeroBank.utility;

namespace SpringHeroBank.model
{
    public class AccountModel
    {
        public Boolean Save(Account account)
        {
            DbConnection.Instance().OpenConnection(); // đảm bảo rằng đã kết nối đến db thành công.
            var salt = Hash.RandomString(7); // sinh ra chuỗi muối random.
            account.Salt = salt; // đưa muối vào thuộc tính của account để lưu vào database.
            // mã hoá password của người dùng kèm theo muối, set thuộc tính password mới.
            account.Password = Hash.GenerateSaltedSHA1(account.Password, account.Salt);
            var sqlQuery = "insert into `account` " +
                           "(`username`, `password`,`salt`, `accountNumber`, `identityCard`, `balance`, `phone`, `email`, `fullName`,  `status`) values" +
                           "(@username, @password, @salt, @accountNumber, @identityCard, @balance, @phone, @email, @fullName,  @status)";
            var cmd = new MySqlCommand(sqlQuery, DbConnection.Instance().Connection);
            cmd.Parameters.AddWithValue("@username", account.Username);
            cmd.Parameters.AddWithValue("@password", account.Password);
            cmd.Parameters.AddWithValue("@accountNumber", account.AccountNumber);
            cmd.Parameters.AddWithValue("@identityCard", account.IdentityCard);
            cmd.Parameters.AddWithValue("@balance", account.Balance);
            cmd.Parameters.AddWithValue("@phone", account.Phone);
            cmd.Parameters.AddWithValue("@email", account.Email);
            cmd.Parameters.AddWithValue("@fullName", account.FullName);
            cmd.Parameters.AddWithValue("@salt", account.Salt);
            cmd.Parameters.AddWithValue("@status", account.Status);
            var result = cmd.ExecuteNonQuery();
            DbConnection.Instance().CloseConnection();
            return result == 1;
        }

        public bool UpdateBalance(Account account, Transaction historyTransaction)
        {
            DbConnection.Instance().OpenConnection(); // đảm bảo rằng đã kết nối đến db thành công.
            var transaction = DbConnection.Instance().Connection.BeginTransaction(); // Khởi tạo transaction.

            try
            {
                /**
                 * 1. Lấy thông tin số dư mới nhất của tài khoản.
                 * 2. Kiểm tra kiểu transaction. Chỉ chấp nhận deposit và withdraw.
                 *     2.1. Kiểm tra số tiền rút nếu kiểu transaction là withdraw.                 
                 * 3. Update số dư vào tài khoản.
                 *     3.1. Tính toán lại số tiền trong tài khoản.
                 *     3.2. Update số tiền vào database.
                 * 4. Lưu thông tin transaction vào bảng transaction.
                 */

                // 1. Lấy thông tin số dư mới nhất của tài khoản.
                var queryBalance = "select balance from `account` where username = @username and status = @status";
                MySqlCommand queryBalanceCommand = new MySqlCommand(queryBalance, DbConnection.Instance().Connection);
                queryBalanceCommand.Parameters.AddWithValue("@username", account.Username);
                queryBalanceCommand.Parameters.AddWithValue("@status", account.Status);
                var balanceReader = queryBalanceCommand.ExecuteReader();
                // Không tìm thấy tài khoản tương ứng, throw lỗi.
                if (!balanceReader.Read())
                {
                    // Không tồn tại bản ghi tương ứng, lập tức rollback transaction, trả về false.
                    // Hàm dừng tại đây.
                    throw new SpringHeroTransactionException("Invalid username");
                }

                // Đảm bảo sẽ có bản ghi.
                var currentBalance = balanceReader.GetDecimal("balance");
                balanceReader.Close();

                // 2. Kiểm tra kiểu transaction. Chỉ chấp nhận deposit và withdraw. 
                if (historyTransaction.Type != Transaction.TransactionType.DEPOSIT
                    && historyTransaction.Type != Transaction.TransactionType.WITHDRAW)
                {
                    throw new SpringHeroTransactionException("Invalid transaction type!");
                }

                // 2.1. Kiểm tra số tiền rút nếu kiểu transaction là withdraw.
                if (historyTransaction.Type == Transaction.TransactionType.WITHDRAW &&
                    historyTransaction.Amount > currentBalance)
                {
                    throw new SpringHeroTransactionException("Not enough money!");
                }

                // 3. Update số dư vào tài khoản.
                // 3.1. Tính toán lại số tiền trong tài khoản.
                if (historyTransaction.Type == Transaction.TransactionType.DEPOSIT)
                {
                    currentBalance += historyTransaction.Amount;
                }
                else
                {
                    currentBalance -= historyTransaction.Amount;
                }

                // 3.2. Update số dư vào database.
                var updateAccountResult = 0;
                var queryUpdateAccountBalance =
                    "update `account` set balance = @balance where username = @username and status = 0";
                var cmdUpdateAccountBalance =
                    new MySqlCommand(queryUpdateAccountBalance, DbConnection.Instance().Connection);
                cmdUpdateAccountBalance.Parameters.AddWithValue("@username", account.Username);
                cmdUpdateAccountBalance.Parameters.AddWithValue("@balance", currentBalance);
                updateAccountResult = cmdUpdateAccountBalance.ExecuteNonQuery();

                // 4. Lưu thông tin transaction vào bảng transaction.
                var insertTransactionResult = 0;
                var queryInsertTransaction = "insert into `transactions` " +
                                             "(id, type, amount, content, senderAccountNumber, receiverAccountNumber, status) " +
                                             "values (@id, @type, @amount, @content, @senderAccountNumber, @receiverAccountNumber, @status)";
                var cmdInsertTransaction =
                    new MySqlCommand(queryInsertTransaction, DbConnection.Instance().Connection);
                cmdInsertTransaction.Parameters.AddWithValue("@id", historyTransaction.Id);
                cmdInsertTransaction.Parameters.AddWithValue("@type", historyTransaction.Type);
                cmdInsertTransaction.Parameters.AddWithValue("@amount", historyTransaction.Amount);
                cmdInsertTransaction.Parameters.AddWithValue("@content", historyTransaction.Content);
                cmdInsertTransaction.Parameters.AddWithValue("@senderAccountNumber",
                    historyTransaction.SenderAccountNumber);
                cmdInsertTransaction.Parameters.AddWithValue("@receiverAccountNumber",
                    historyTransaction.ReceiverAccountNumber);
                cmdInsertTransaction.Parameters.AddWithValue("@status", historyTransaction.Status);
                insertTransactionResult = cmdInsertTransaction.ExecuteNonQuery();

                if (updateAccountResult == 1 && insertTransactionResult == 1)
                {
                    transaction.Commit();
                    return true;
                }
            }
            catch (SpringHeroTransactionException e)
            {
                transaction.Rollback();
                return false;
            }

            DbConnection.Instance().CloseConnection();
            return false;
        }

        public Boolean CheckExistUserName(string username)
        {
            return false;
        }

        public Account GetAccountByUserName(string username)
        {
            DbConnection.Instance().OpenConnection();
            var queryString = "select * from  `account` where username = @username and status = 0";
            var cmd = new MySqlCommand(queryString, DbConnection.Instance().Connection);
            cmd.Parameters.AddWithValue("@username", username);
            var reader = cmd.ExecuteReader();
            Account account = null;
            if (reader.Read())
            {
                var _username = reader.GetString("username");
                var password = reader.GetString("password");
                var salt = reader.GetString("salt");
                var accountNumber = reader.GetString("accountNumber");
                var identityCard = reader.GetString("identityCard");
                var balance = reader.GetDecimal("balance");
                var phone = reader.GetString("phone");
                var email = reader.GetString("email");
                var fullName = reader.GetString("fullName");
                var createdAt = reader.GetString("createdAt");
                var updatedAt = reader.GetString("updatedAt");
                var status = reader.GetInt32("status");
                account = new Account(_username, password, salt, accountNumber, identityCard, balance, phone, email,
                    fullName, createdAt, updatedAt, (Account.ActiveStatus) status);
            }

            DbConnection.Instance().CloseConnection();
            return account;
        }
    


             public Account GetAccountByFullName(string fullname)
             {
                 DbConnection.Instance().OpenConnection();
                 var queryString = "select * from  `account` where fullName = @fullName ";
                 var cmd = new MySqlCommand(queryString, DbConnection.Instance().Connection);
                 cmd.Parameters.AddWithValue("@fullname", fullname);
                 var reader = cmd.ExecuteReader();
                 Account account = null;
                 if (reader.Read())
                 {
                     var _username = reader.GetString("username");
                     var password = reader.GetString("password");
                     var salt = reader.GetString("salt");
                     var accountNumber = reader.GetString("accountNumber");
                     var identityCard = reader.GetString("identityCard");
                     var balance = reader.GetDecimal("balance");
                     var phone = reader.GetString("phone");
                     var email = reader.GetString("email");
                     var fullName = reader.GetString("fullName");
                     var createdAt = reader.GetString("createdAt");
                     var updatedAt = reader.GetString("updatedAt");
                     var status = reader.GetInt32("status");
                     account = new Account(_username, password, salt, accountNumber, identityCard, balance, phone, email,
                         fullName, createdAt, updatedAt, (Account.ActiveStatus) status);
                 }

                 DbConnection.Instance().CloseConnection();
                 return account;
             }
    }
}