-- Create Users table (authentication/login users)
CREATE TABLE Users (
    UserId INT IDENTITY(1,1) PRIMARY KEY,
    Username NVARCHAR(50) NOT NULL UNIQUE,
    Email NVARCHAR(100) NOT NULL UNIQUE,
    PasswordHash NVARCHAR(255) NOT NULL,
    CreatedDate DATETIME2 DEFAULT GETDATE(),
    LastLoginDate DATETIME2,
    IsActive BIT DEFAULT 1
);
GO

-- Create Customers table (business entities that place orders)
CREATE TABLE Customers (
    CustomerId INT IDENTITY(1,1) PRIMARY KEY,
    UserId INT NOT NULL,
    CompanyName NVARCHAR(100),
    FirstName NVARCHAR(50) NOT NULL,
    LastName NVARCHAR(50) NOT NULL,
    Phone NVARCHAR(20),
    Address NVARCHAR(200),
    City NVARCHAR(50),
    State NVARCHAR(50),
    ZipCode NVARCHAR(10),
    Country NVARCHAR(50) DEFAULT 'USA',
    CreatedDate DATETIME2 DEFAULT GETDATE(),
    CONSTRAINT FK_Customers_Users FOREIGN KEY (UserId) REFERENCES Users(UserId)
);
GO

-- Create OrderHeaders table (order header/master records)
CREATE TABLE OrderHeaders (
    OrderId INT IDENTITY(1,1) PRIMARY KEY,
    CustomerId INT NOT NULL,
    OrderNumber NVARCHAR(20) NOT NULL UNIQUE,
    OrderDate DATETIME2 DEFAULT GETDATE(),
    RequiredDate DATETIME2,
    ShippedDate DATETIME2,
    OrderStatus NVARCHAR(20) DEFAULT 'Pending',
    SubTotal DECIMAL(10,2) NOT NULL DEFAULT 0,
    TaxAmount DECIMAL(10,2) NOT NULL DEFAULT 0,
    ShippingAmount DECIMAL(10,2) NOT NULL DEFAULT 0,
    TotalAmount DECIMAL(10,2) NOT NULL DEFAULT 0,
    Notes NVARCHAR(500),
    CreatedDate DATETIME2 DEFAULT GETDATE(),
    CONSTRAINT FK_OrderHeaders_Customers FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId),
    CONSTRAINT CK_OrderStatus CHECK (OrderStatus IN ('Pending', 'Processing', 'Shipped', 'Delivered', 'Cancelled'))
);
GO

-- Insert sample data
INSERT INTO Users (Username, Email, PasswordHash) VALUES 
('john.doe', 'john.doe@email.com', 'hashed_password_123'),
('jane.smith', 'jane.smith@email.com', 'hashed_password_456');
GO

INSERT INTO Customers (UserId, FirstName, LastName, CompanyName, Phone, Address, City, State, ZipCode) VALUES
(1, 'John', 'Doe', 'Acme Corp', '555-0123', '123 Main St', 'Anytown', 'CA', '90210'),
(2, 'Jane', 'Smith', 'Tech Solutions Inc', '555-0124', '456 Oak Ave', 'Somewhere', 'TX', '75201');
GO

INSERT INTO OrderHeaders (CustomerId, OrderNumber, SubTotal, TaxAmount, ShippingAmount, TotalAmount) VALUES
(1, 'ORD-2024-001', 100.00, 8.50, 15.00, 123.50),
(2, 'ORD-2024-002', 250.00, 21.25, 20.00, 291.25);
GO