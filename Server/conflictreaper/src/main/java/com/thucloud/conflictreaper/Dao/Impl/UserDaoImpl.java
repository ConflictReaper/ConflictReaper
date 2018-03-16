package com.thucloud.conflictreaper.Dao.Impl;

import org.apache.ibatis.session.SqlSession;
import org.springframework.beans.factory.annotation.Autowired;

import com.thucloud.conflictreaper.Dao.UserDao;
import com.thucloud.conflictreaper.Model.User;

public class UserDaoImpl implements UserDao {
	@Autowired
	private SqlSession sqlSession;
	public SqlSession getSqlSession() {
		return sqlSession;
	}
	public void setSqlSession(SqlSession sqlSession) {
		this.sqlSession = sqlSession;
	}

	public int deleteByPrimaryKey(Integer id) {
		return sqlSession.delete("com.thucloud.conflictreaper.Dao.UserDao.deleteByPrimaryKey", id);
	}

	public int insert(User record) {
		return sqlSession.insert("com.thucloud.conflictreaper.Dao.UserDao.insert", record);
	}

	public int insertSelective(User record) {
		return sqlSession.insert("com.thucloud.conflictreaper.Dao.UserDao.insertSelective", record);
	}

	public User selectByPrimaryKey(Integer id) {
		return sqlSession.selectOne("com.thucloud.conflictreaper.Dao.UserDao.selectByPrimaryKey", id);
	}

	public int updateByPrimaryKeySelective(User record) {
		return sqlSession.update("com.thucloud.conflictreaper.Dao.UserDao.updateByPrimaryKeySelective", record);
	}

	public int updateByPrimaryKey(User record) {
		return sqlSession.update("com.thucloud.conflictreaper.Dao.UserDao.updateByPrimaryKey", record);
	}
	
	public User selectByUserName(String username) {
		return sqlSession.selectOne("com.thucloud.conflictreaper.Dao.UserDao.selectByUserName", username);
	}

}
