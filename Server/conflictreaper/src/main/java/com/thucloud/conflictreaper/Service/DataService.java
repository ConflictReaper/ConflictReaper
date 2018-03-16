package com.thucloud.conflictreaper.Service;

public interface DataService {
	public String getAddressMap(String userCollection);
	public void register(String name, String ip, int port, int status);
	public void updateStatus(String name, int status);
}
