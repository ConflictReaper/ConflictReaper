package com.thucloud.conflictreaper.Dao;

import com.thucloud.conflictreaper.Model.User;

public interface UserDao {
    int deleteByPrimaryKey(Integer id);

    int insert(User record);

    int insertSelective(User record);

    User selectByPrimaryKey(Integer id);
    
    User selectByUserName(String username);

    int updateByPrimaryKeySelective(User record);

    int updateByPrimaryKey(User record);
}