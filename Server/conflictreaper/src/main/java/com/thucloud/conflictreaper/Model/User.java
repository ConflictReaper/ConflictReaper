package com.thucloud.conflictreaper.Model;

public class User {
    private Integer id;

    private String name;

    private String ip;

    private Integer port;

    private Integer status;
    
    public User() {
    	this.id = null;
    	this.name = null;
    	this.ip = null;
    	this.port = null;
    	this.status = null;
    }
    
    public User(String name, String ip, int port, int status) {
    	this.id = null;
    	this.name = name;
    	this.ip = ip;
    	this.port = port;
    	this.status = status;
    }

    public Integer getId() {
        return id;
    }

    public void setId(Integer id) {
        this.id = id;
    }

    public String getName() {
        return name;
    }

    public void setName(String name) {
        this.name = name == null ? null : name.trim();
    }

    public String getIp() {
        return ip;
    }

    public void setIp(String ip) {
        this.ip = ip == null ? null : ip.trim();
    }

    public Integer getPort() {
        return port;
    }

    public void setPort(Integer port) {
        this.port = port;
    }

    public Integer getStatus() {
        return status;
    }

    public void setStatus(Integer status) {
        this.status = status;
    }
}