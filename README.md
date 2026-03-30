# 📚 Library Management System

A simple **Library Management Web Application** built with **ASP.NET Core MVC**, designed to manage books, readers, and book loans efficiently.

---

## ✨ Features

- 📖 **Book Management**
  - Add, edit, delete books
  - Manage quantity & availability
  - Search by title, author, publication year

- 👤 **Reader Management**
  - Manage reader information
  - Email & phone number validation

- 🔄 **Loan Management**
  - Create loan records
  - Return books
  - Track loan status (Borrowing / Returned / Lost / Damaged)
  - Update book quantity automatically

- 🪪 **Library Card Management**
  - Issue library cards for readers
  - Manage card status (Active / Expired / Blocked)
  - Link cards with readers

- 🔍 **Search**
  - Quick search across books, readers, and loans

---

## 🛠️ Tech Stack

- **ASP.NET Core MVC (.NET 9)**
- **Entity Framework Core**
- **SQLite**
- **Bootstrap**

---

## 🏗️ Architecture

- MVC (Model – View – Controller)
- Code First with Entity Framework Core
- Service Layer (Business Logic Separation)

---

## 🗄️ Database

- Database: **SQLite**
- File: `library.db`
- Includes **sample data for demo**
- Automatically updated via Entity Framework Migrations

---

## 🚀 Getting Started

### 1️⃣ Clone the repository
```bash
git clone https://github.com/hieumaymin/Library-Management.git
cd Library-Management

### 2️⃣ Restore dependencies
```bash
dotnet restore

### 3️⃣ Run the application 
```bash
dotnet run

###4️⃣ Open in browser
```bash
https://localhost:xxxx

The port number (xxxx) will be shown in the terminal after running dotnet run.


##⏹️ Stop the application
Press: Ctrl + C
in the terminal to stop the running application.
