﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace secureshare.Models;

public partial class Department
{
    [Key]
    public int DepartmentID { get; set; }

    [StringLength(255)]
    public string DepartmentName { get; set; }
}