package com.thucloud.conflictreaper.Service.Impl;

import org.springframework.beans.factory.annotation.Autowired;

import com.thucloud.conflictreaper.Dao.UserDao;
import com.thucloud.conflictreaper.Model.User;
import com.thucloud.conflictreaper.Service.DataService;
import com.thucloud.conflictreaper.WebSocket.ChatWebSocketHandler;

public class DataServiceImpl implements DataService {
	@Autowired
	private UserDao userDao;

	public UserDao getUserDao() {
		return userDao;
	}

	public void setUserDao(UserDao userDao) {
		this.userDao = userDao;
	}

	public String getAddressMap(String userCollection) {
		String[] users = userCollection.split(",");
		String map = "";
		for (int i = 0; i < users.length; i++) {
			User result = userDao.selectByUserName(users[i]);
			if (result != null && result.getStatus() == 1 && ChatWebSocketHandler.isOnline(users[i])) {
				String pair = users[i] + "|";
				pair += result.getIp() + ":" + result.getPort();
				map += pair + ",";
			}
		}
		if (map.length() > 0)
			map = map.substring(0, map.length() - 1);
		return map;
	}

	public void register(String name, String ip, int port, int status) {
		User result = userDao.selectByUserName(name);
		if (result == null) {
			userDao.insertSelective(new User(name, ip, port, status));
		}
		else {
			result.setIp(ip);
			result.setPort(port);
			result.setStatus(status);
			userDao.updateByPrimaryKey(result);
		}
	}

	public void updateStatus(String name, int status) {
		User user = userDao.selectByUserName(name);
		if (user != null) {
			user.setStatus(status);
			userDao.updateByPrimaryKey(user);
		}
	}
}
