using CVSWithLibary;
using LoggingWithStreamWriter;
using System.Globalization;
using System.Reflection.Metadata;

const string UsersFilePath = "Users.txt";
const string LogFilePath = "log.txt";
const string PeopleFilePath = "people.csv";
const int MaxLoginAttempts = 3;

LogWriter _logWriter = null!;
string _currentUser = string.Empty;
Dictionary<string, int> _loginAttempts = new Dictionary<string, int>();

if (!InitializeAndAuthenticateUser())
{
    Console.WriteLine("Authentication or initial setup failed. Program will exit.");
    _logWriter?.WriteLog("ERROR", "Authentication or initial user setup failed. Application terminated.");
    _logWriter?.Dispose();
    return;
}

var helper = new CsvHelperExample();
var readPeople = helper.Read(PeopleFilePath).ToList();

try
{
    _logWriter.WriteLog("INFO", $"User: {_currentUser} - Application started.");
    var opc = "0";
    do
    {
        opc = Menu();
        Console.WriteLine("=======================================");
        switch (opc)
        {
            case "1":
                ShowContent();
                break;
            case "2":
                AddPerson();
                break;
            case "3":
                SaveChanges();
                Console.WriteLine("Changes saved successfully.");
                break;
            case "4":
                EditPerson();
                break;
            case "5":
                DeletePerson();
                break;
            case "6":
                ShowReportByCity();
                break;
            case "7":
                CreateSystemUser();
                break;
            case "0":
                _logWriter.WriteLog("INFO", $"User: {_currentUser} - User chose to exit.");
                break;
            default:
                Console.WriteLine("Invalid option. Try again.");
                _logWriter.WriteLog("WARN", $"User: {_currentUser} - Invalid menu option selected: {opc}");
                break;
        }
    } while (opc != "0");
    SaveChanges();
    _logWriter.WriteLog("INFO", $"User: {_currentUser} - Application shutting down. Final changes saved.");
}
catch (Exception ex)
{
    Console.WriteLine($"An unexpected error occurred: {ex.Message}");
    if (_logWriter != null) 
    {
        _logWriter.WriteLog("FATAL", $"User: {_currentUser} - An unhandled exception occurred: {ex.Message} {ex.StackTrace}");
    }
    else
    {
        try { File.AppendAllText(LogFilePath, $"{DateTime.Now:s} [FATAL] An unhandled exception occurred before full log initialization: {ex.Message}{Environment.NewLine}"); }
        catch { }
    }
}
finally
{
    _logWriter?.Dispose();
}

bool InitializeAndAuthenticateUser()
{
    try
    {
        _logWriter = new LogWriter(LogFilePath);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Critical error initializing log file: {ex.Message}. Program cannot continue safely.");
        
        try { File.AppendAllText(LogFilePath, $"{DateTime.Now:s} [FATAL] Critical error initializing LogWriter: {ex.Message}{Environment.NewLine}"); }
        catch {}
        return false;
    }


    if (!File.Exists(UsersFilePath) || new FileInfo(UsersFilePath).Length == 0)
    {
        Console.WriteLine(File.Exists(UsersFilePath) ? "El archivo de usuarios ('Users.txt') está vacío." : "El archivo de usuarios ('Users.txt') no existe.");
        _logWriter.WriteLog("INFO", "Users.txt not found or empty. Initiating first admin user creation process.");
        Console.WriteLine("Se procederá a crear el primer usuario administrador.");
        return CreateInitialAdminUser();
    }

    for (int i = 0; i < MaxLoginAttempts; i++)
    {
        Console.Write("Enter username: ");
        string? username = Console.ReadLine();
        Console.Write("Enter password: ");
        string? password = Console.ReadLine();

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            Console.WriteLine("Username and password cannot be empty.");
            _logWriter.WriteLog("WARN", $"Login attempt with empty username or password. Attempt {i + 1} of {MaxLoginAttempts}.");
            if (i == MaxLoginAttempts - 1 && !string.IsNullOrEmpty(username))
            {
                _logWriter.WriteLog("ERROR", $"User: {username} - Final login attempt failed due to empty credentials. No blocking action taken as user/pass empty.");
            }
            else if (i == MaxLoginAttempts - 1)
            {
                _logWriter.WriteLog("ERROR", $"Final login attempt failed due to empty credentials. No blocking action taken.");
            }
            continue;
        }

        var users = ReadUsers();
        var userEntry = users.FirstOrDefault(u => u.username.Equals(username, StringComparison.OrdinalIgnoreCase));

        if (userEntry == default)
        {
            Console.WriteLine("Invalid username or password.");
            _logWriter.WriteLog("WARN", $"Login attempt for non-existent user: {username}. Attempt {i + 1} of {MaxLoginAttempts}.");
            IncrementFailedAttempts(username); 
            if (_loginAttempts.ContainsKey(username) && _loginAttempts[username] >= MaxLoginAttempts)
            {
                _logWriter.WriteLog("ERROR", $"User: {username} - Non-existent user reached max pseudo-attempts. No blocking action taken.");
                Console.WriteLine("Maximum login attempts reached for this username.");
                return false;
            }
            continue;
        }

        if (!userEntry.isActive)
        {
            Console.WriteLine("User account is locked.");
            _logWriter.WriteLog("ERROR", $"Login attempt for locked user: {username}.");
            return false;
        }

        if (userEntry.password == password)
        {
            _currentUser = username;
            Console.WriteLine($"Welcome, {_currentUser}!");
            _logWriter.WriteLog("INFO", $"User: {_currentUser} - Login successful.");
            if (_loginAttempts.ContainsKey(username)) _loginAttempts.Remove(username);
            return true;
        }
        else
        {
            Console.WriteLine("Invalid username or password.");
            _logWriter.WriteLog("WARN", $"User: {username} - Failed login attempt (incorrect password). Attempt {IncrementFailedAttempts(username)} of {MaxLoginAttempts}.");
            if (_loginAttempts[username] >= MaxLoginAttempts)
            {
                Console.WriteLine("Maximum login attempts reached. User account will be locked.");
                LockUser(username, users);
                _logWriter.WriteLog("ERROR", $"User: {username} - Account locked after {MaxLoginAttempts} failed login attempts.");
                return false;
            }
        }
    }
    _logWriter.WriteLog("ERROR", "Maximum login attempts reached overall for the session.");
    Console.WriteLine("Maximum login attempts reached.");
    return false;
}

bool CreateInitialAdminUser()
{
    Console.WriteLine("--- Creación del Primer Usuario Administrador ---");
    string newUsername;
    while (true)
    {
        Console.Write("Ingrese el nombre de usuario para el administrador: ");
        newUsername = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(newUsername)) break;
        Console.WriteLine("El nombre de usuario no puede estar vacío.");
    }

    Console.Write("Ingrese la contraseña para el administrador: ");
    string newPassword = Console.ReadLine() ?? string.Empty;
    while (string.IsNullOrEmpty(newPassword))
    {
        Console.WriteLine("La contraseña no puede estar vacía.");
        Console.Write("Ingrese la contraseña para el administrador: ");
        newPassword = Console.ReadLine() ?? string.Empty;
    }


    var initialUsers = new List<(string username, string password, bool isActive)>
    {
        (newUsername, newPassword, true)
    };
    WriteUsers(initialUsers);

    _currentUser = newUsername;
    Console.WriteLine($"Usuario administrador '{_currentUser}' creado exitosamente. Bienvenido.");
    _logWriter.WriteLog("INFO", $"Initial admin user '{_currentUser}' created and logged in.");
    return true;
}


int IncrementFailedAttempts(string username)
{
    if (_loginAttempts.ContainsKey(username))
    {
        _loginAttempts[username]++;
    }
    else
    {
        _loginAttempts[username] = 1;
    }
    return _loginAttempts[username];
}

List<(string username, string password, bool isActive)> ReadUsers()
{
    var users = new List<(string username, string password, bool isActive)>();
    if (!File.Exists(UsersFilePath)) return users;

    try
    {
        var lines = File.ReadAllLines(UsersFilePath);
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var parts = line.Split(',');
            if (parts.Length == 3 && bool.TryParse(parts[2], out bool isActive))
            {
                users.Add((parts[0], parts[1], isActive));
            }
            else
            {
                _logWriter?.WriteLog("WARN", $"Malformed line in Users.txt: {line}");
            }
        }
    }
    catch (Exception ex)
    {
        _logWriter?.WriteLog("ERROR", $"Error reading Users.txt: {ex.Message}");
    }
    return users;
}

void WriteUsers(List<(string username, string password, bool isActive)> users)
{
    try
    {
        var lines = users.Select(u => $"{u.username},{u.password},{u.isActive.ToString().ToLower()}");
        File.WriteAllLines(UsersFilePath, lines);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error writing to Users.txt: {ex.Message}");
        _logWriter?.WriteLog("ERROR", $"Error writing to Users.txt: {ex.Message}");
    }
}

void LockUser(string username, List<(string username, string password, bool isActive)> users)
{
    var userIndex = users.FindIndex(u => u.username.Equals(username, StringComparison.OrdinalIgnoreCase));
    if (userIndex != -1)
    {
        var user = users[userIndex];
        users[userIndex] = (user.username, user.password, false);
        WriteUsers(users);
    }
}

void CreateSystemUser()
{
    Console.WriteLine("--- Crear Nuevo Usuario del Sistema ---");
    var users = ReadUsers();
    string newUsername;

    while (true)
    {
        Console.Write("Ingrese el nombre del nuevo usuario: ");
        newUsername = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(newUsername))
        {
            Console.WriteLine("El nombre de usuario no puede estar vacío.");
            continue;
        }
        if (users.Any(u => u.username.Equals(newUsername, StringComparison.OrdinalIgnoreCase)))
        {
            Console.WriteLine("Error: Este nombre de usuario ya existe. Intente con otro.");
        }
        else
        {
            break;
        }
    }

    Console.Write("Ingrese la contraseña para el nuevo usuario: ");
    string newPassword = Console.ReadLine() ?? string.Empty;
    while (string.IsNullOrEmpty(newPassword))
    {
        Console.WriteLine("La contraseña no puede estar vacía.");
        Console.Write("Ingrese la contraseña para el nuevo usuario: ");
        newPassword = Console.ReadLine() ?? string.Empty;
    }

    Console.Write("¿El usuario estará activo? (true/false, presione ENTER para 'true'): ");
    bool isActive = true; 
    string? activeInput = Console.ReadLine();
    if (!string.IsNullOrWhiteSpace(activeInput) && !bool.TryParse(activeInput, out isActive))
    {
        Console.WriteLine("Entrada inválida para estado activo, se establecerá como 'false' (inactivo).");
        isActive = false;
    }
    else if (string.IsNullOrWhiteSpace(activeInput))
    {
        isActive = true; 
    }


    users.Add((newUsername, newPassword, isActive));
    WriteUsers(users);
    Console.WriteLine($"Usuario '{newUsername}' creado exitosamente. Estado activo: {isActive}");
    _logWriter.WriteLog("INFO", $"User: {_currentUser} - Created new system user: {newUsername}, Active: {isActive}");
}


void ShowContent()
{
    if (!readPeople.Any())
    {
        Console.WriteLine("No people data to display.");
        _logWriter.WriteLog("INFO", $"User: {_currentUser} - Viewed content (no data).");
        return;
    }
    foreach (var person in readPeople)
    {
        Console.WriteLine(person);
    }
    _logWriter.WriteLog("INFO", $"User: {_currentUser} - Viewed content ({readPeople.Count} records).");
}

void AddPerson()
{
    Console.WriteLine("--- Add New Person ---");
    int id;
    while (true)
    {
        Console.Write("Enter the ID: ");
        string? idStr = Console.ReadLine();
        if (int.TryParse(idStr, out id))
        {
            if (readPeople.Any(p => p.Id == id))
            {
                Console.WriteLine("Error: This ID already exists. Please enter a unique ID.");
            }
            else
            {
                break;
            }
        }
        else
        {
            Console.WriteLine("Error: ID must be a valid number.");
        }
    }

    string? firstName;
    do
    {
        Console.Write("Enter the First name: ");
        firstName = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(firstName))
        {
            Console.WriteLine("Error: First name cannot be empty.");
        }
    } while (string.IsNullOrWhiteSpace(firstName));

    string? lastName;
    do
    {
        Console.Write("Enter the Last name: ");
        lastName = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(lastName))
        {
            Console.WriteLine("Error: Last name cannot be empty.");
        }
    } while (string.IsNullOrWhiteSpace(lastName));

    string? phone;
    do
    {
        Console.Write("Enter the phone (digits only): ");
        phone = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(phone) || !phone.All(char.IsDigit))
        {
            Console.WriteLine("Error: Phone must be a non-empty numeric value.");
            phone = null;
        }
    } while (phone == null);

    Console.Write("Enter the city: ");
    var city = Console.ReadLine();

    decimal balance;
    while (true)
    {
        Console.Write("Enter the balance: ");
        string? balanceStr = Console.ReadLine();
        if (decimal.TryParse(balanceStr, NumberStyles.Any, CultureInfo.InvariantCulture, out balance) || decimal.TryParse(balanceStr, out balance)) // More flexible parsing
        {
            if (balance >= 0)
            {
                break;
            }
            else
            {
                Console.WriteLine("Error: Balance must be a positive number (or zero).");
            }
        }
        else
        {
            Console.WriteLine("Error: Balance must be a valid number.");
        }
    }

    var newPerson = new Person
    {
        Id = id,
        FirstName = firstName ?? string.Empty,
        LastName = lastName ?? string.Empty,
        Phone = phone ?? string.Empty,
        City = city ?? string.Empty,
        Balance = balance
    };

    readPeople.Add(newPerson);
    Console.WriteLine("Person added successfully.");
    _logWriter.WriteLog("INFO", $"User: {_currentUser} - Added new person. ID: {newPerson.Id}, Name: {newPerson.FirstName} {newPerson.LastName}");
}


void EditPerson()
{
    Console.Write("Enter the ID of the person to edit: ");
    string? idStr = Console.ReadLine();
    if (!int.TryParse(idStr, out int idToEdit))
    {
        Console.WriteLine("Invalid ID format.");
        _logWriter.WriteLog("WARN", $"User: {_currentUser} - Edit person attempt with invalid ID format: {idStr}");
        return;
    }

    var personToEdit = readPeople.FirstOrDefault(p => p.Id == idToEdit);
    if (personToEdit == null)
    {
        Console.WriteLine($"Person with ID {idToEdit} not found.");
        _logWriter.WriteLog("WARN", $"User: {_currentUser} - Edit person attempt, ID not found: {idToEdit}");
        return;
    }

    Console.WriteLine($"Editing person: {personToEdit.FirstName} {personToEdit.LastName} (ID: {personToEdit.Id})");
    Console.WriteLine("Press ENTER to keep current value for a field.");

    string? tempInput;

    Console.Write($"Enter new First name (current: {personToEdit.FirstName}): ");
    tempInput = Console.ReadLine();
    if (!string.IsNullOrWhiteSpace(tempInput))
    {
        personToEdit.FirstName = tempInput;
    }
    else if (tempInput == "")
    {
        
    }
    else 
    {
     
    }


    Console.Write($"Enter new Last name (current: {personToEdit.LastName}): ");
    tempInput = Console.ReadLine();
    if (!string.IsNullOrWhiteSpace(tempInput))
    {
        personToEdit.LastName = tempInput;
    }

    while (true)
    {
        Console.Write($"Enter new phone (current: {personToEdit.Phone}): ");
        tempInput = Console.ReadLine();
        if (tempInput == "") break;
        if (string.IsNullOrWhiteSpace(tempInput)) 
        {
            Console.WriteLine("Phone cannot be just whitespace if changed. Keeping current value.");
            break;
        }
        if (tempInput.All(char.IsDigit))
        {
            personToEdit.Phone = tempInput;
            break;
        }
        Console.WriteLine("Error: Phone must be a non-empty numeric value if changed.");
    }


    Console.Write($"Enter new city (current: {personToEdit.City}): ");
    tempInput = Console.ReadLine();
    if (!string.IsNullOrWhiteSpace(tempInput))
    {
        personToEdit.City = tempInput;
    }
    else if (tempInput == "")
    {
    }

    while (true)
    {
        Console.Write($"Enter new balance (current: {personToEdit.Balance:N2}): ");
        tempInput = Console.ReadLine();
        if (tempInput == "") break; 
        if (string.IsNullOrWhiteSpace(tempInput))
        {
            Console.WriteLine("Balance cannot be just whitespace if changed. Keeping current value.");
            break;
        }
        if (decimal.TryParse(tempInput, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal newBalance) || decimal.TryParse(tempInput, out newBalance))
        {
            if (newBalance >= 0)
            {
                personToEdit.Balance = newBalance;
                break;
            }
            Console.WriteLine("Error: Balance must be a positive number (or zero) if changed.");
        }
        else
        {
            Console.WriteLine("Error: Balance must be a valid number if changed.");
        }
    }

    Console.WriteLine("Person updated successfully.");
    _logWriter.WriteLog("INFO", $"User: {_currentUser} - Edited person. ID: {personToEdit.Id}");
}

void DeletePerson()
{
    Console.Write("Enter the ID of the person to delete: ");
    string? idStr = Console.ReadLine();
    if (!int.TryParse(idStr, out int idToDelete))
    {
        Console.WriteLine("Invalid ID format.");
        _logWriter.WriteLog("WARN", $"User: {_currentUser} - Delete person attempt with invalid ID format: {idStr}");
        return;
    }

    var personToDelete = readPeople.FirstOrDefault(p => p.Id == idToDelete);
    if (personToDelete == null)
    {
        Console.WriteLine($"Person with ID {idToDelete} not found.");
        _logWriter.WriteLog("WARN", $"User: {_currentUser} - Delete person attempt, ID not found: {idToDelete}");
        return;
    }

    Console.WriteLine("Person found:");
    Console.WriteLine(personToDelete);
    Console.Write("Are you sure you want to delete this person? (yes/no): ");
    string? confirmation = Console.ReadLine();

    if (confirmation?.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase) == true)
    {
        readPeople.Remove(personToDelete);
        Console.WriteLine("Person deleted successfully.");
        _logWriter.WriteLog("INFO", $"User: {_currentUser} - Deleted person. ID: {personToDelete.Id}, Name: {personToDelete.FirstName} {personToDelete.LastName}");
    }
    else
    {
        Console.WriteLine("Deletion cancelled.");
        _logWriter.WriteLog("INFO", $"User: {_currentUser} - Deletion cancelled for person ID: {personToDelete.Id}");
    }
}

void ShowReportByCity()
{
    if (!readPeople.Any())
    {
        Console.WriteLine("No data to generate report.");
        _logWriter.WriteLog("INFO", $"User: {_currentUser} - Requested report by city (no data).");
        return;
    }

    var reportData = readPeople
        .GroupBy(p => string.IsNullOrWhiteSpace(p.City) ? "N/A" : p.City)
        .Select(g => new
        {
            CityName = g.Key,
            PeopleInCity = g.ToList(),
            TotalBalanceInCity = g.Sum(p => p.Balance)
        })
        .OrderBy(g => g.CityName);

    Console.WriteLine("\n--- Report by City ---");
    decimal grandTotalBalance = 0;

    foreach (var cityGroup in reportData)
    {
        Console.WriteLine($"\nCiudad: {cityGroup.CityName}\n");
        Console.WriteLine("ID\tNombres\t\tApellidos\tSaldo");
        Console.WriteLine("—\t—---------\t—--------\t—--------");

        foreach (var person in cityGroup.PeopleInCity)
        {
            Console.WriteLine($"{person.Id}\t{person.FirstName,-10}\t{person.LastName,-10}\t{person.Balance,10:N2}");
        }
        Console.WriteLine($"\t\t\t\t\t=========");
        Console.WriteLine($"Total: {cityGroup.CityName,-15}\t\t\t{cityGroup.TotalBalanceInCity,10:N2}");
        grandTotalBalance += cityGroup.TotalBalanceInCity;
    }

    Console.WriteLine("\n\t\t\t\t\t=========");
    Console.WriteLine($"Total General:\t\t\t\t{grandTotalBalance,10:N2}");
    _logWriter.WriteLog("INFO", $"User: {_currentUser} - Generated report by city. Grand Total: {grandTotalBalance:N2}");
}

void SaveChanges()
{
    helper.Write(PeopleFilePath, readPeople);
    _logWriter.WriteLog("INFO", $"User: {_currentUser} - Changes saved to {PeopleFilePath}.");
}

string Menu()
{
    Console.WriteLine("\n=======================================");
    Console.WriteLine("1. Show content");
    Console.WriteLine("2. Add person");
    Console.WriteLine("3. Save changes");
    Console.WriteLine("4. Edit person");
    Console.WriteLine("5. Delete person");
    Console.WriteLine("6. Show report by city");
    Console.WriteLine("7. Create new system user");
    Console.WriteLine("0. Exit");
    Console.Write("Choose an option: ");
    return Console.ReadLine() ?? "0";
}